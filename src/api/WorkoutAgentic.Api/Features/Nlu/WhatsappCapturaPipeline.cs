using Microsoft.Extensions.Logging;
using WorkoutAgentic.Api.Features.WhatsappMedia;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.Nlu;

// Analizador del slice NLU (etapas 1→3 + resolver, ADR-0002 S1/S2). Cada
// etapa separada y con su tipo de resultado; si una etapa falla devuelve null
// SIN dato inventado. La línea de log es la huella auditable de la
// interpretación (reglas de negocio §4); el slice de sesión la consume vía
// IMensajesNlu para aplicar el estado y responder al alumno.
internal sealed class WhatsappCapturaPipeline(
    IWhatsappAudioDownloader audioDownloader,
    ISpeechToText speechToText,
    IIntentClassifier classifier,
    IEntityExtractor extractor,
    IWhatsappCatalogoEjercicios catalogo,
    ILogger<WhatsappCapturaPipeline> logger) : IMensajesNlu
{
    public async Task<AnalisisCaptura?> AnalizarAsync(
        IncomingWhatsappMessage message, CancellationToken cancellationToken)
    {
        string texto;
        TranscripcionAudio? transcripcion = null;

        switch (message.Kind)
        {
            case WhatsappMessageKind.Text:
                if (string.IsNullOrWhiteSpace(message.TextBody))
                {
                    logger.LogWarning(
                        "Mensaje de texto sin cuerpo: id={Id} from={From}.", message.MessageId, message.From);
                    return null;
                }

                texto = message.TextBody.Trim();
                break;

            case WhatsappMessageKind.Audio:
                if (string.IsNullOrWhiteSpace(message.AudioMediaId))
                {
                    logger.LogWarning(
                        "Nota de voz sin media id: id={Id} from={From}.", message.MessageId, message.From);
                    return null;
                }

                var archivo = await audioDownloader.DescargarAsync(message.AudioMediaId, cancellationToken);
                if (!archivo.Exito || archivo.Bytes is not { Length: > 0 } bytes)
                {
                    logger.LogWarning(
                        "Descarga del audio falló: id={Id} media={Media} título={Titulo}.",
                        message.MessageId, message.AudioMediaId, archivo.ErrorTitulo);
                    return null; // el reenvío por WhatsApp viene con la máquina de estados.
                }

                transcripcion = await speechToText.TranscribirAsync(
                    bytes, archivo.MimeType ?? "audio/ogg", cancellationToken);
                if (!transcripcion.Exito || string.IsNullOrWhiteSpace(transcripcion.Texto))
                {
                    logger.LogWarning(
                        "Transcripción falló: id={Id} título={Titulo}.",
                        message.MessageId, transcripcion.ErrorTitulo);
                    return null;
                }

                texto = transcripcion.Texto.Trim();
                break;

            default:
                logger.LogInformation(
                    "Mensaje omitido por el pipeline (kind={Kind}): id={Id}.", message.Kind, message.MessageId);
                return null;
        }

        // Etapas 2 y 3: una sola llamada al LLM (misma instancia scoped).
        var clasificacion = await classifier.ClasificarAsync(texto, cancellationToken);
        var entidades = await extractor.ExtraerAsync(texto, cancellationToken);

        // Resolver determinístico SOLO si hay ejercicio crudo que resolver.
        ResultadoResolucionEjercicio? resolucion = null;
        if (entidades.Exito && entidades.Entidades?.EjercicioTexto is { Length: > 0 } crudo)
        {
            resolucion = CatalogoEjercicioResolver.Resolver(
                crudo, await catalogo.LeerAsync(cancellationToken));
        }

        var analisis = new AnalisisCaptura(
            message.Kind,
            texto,
            transcripcion,
            clasificacion,
            entidades.Exito ? entidades.Entidades : null,
            resolucion);

        // Línea auditable (reglas de negocio §4: el texto crudo queda para
        // rastrear desvíos). Ningún dato inventado: los faltantes muestran '-'.
        logger.LogInformation(
            "Captura interpretada: msg={Id} kind={Kind} texto='{Texto}' intent={Intento} " +
            "(conf={Confianza}) ejercicio='{Ejercicio}' resolver={Estado} ejercicioId={EjercicioId} " +
            "serie={Serie} cantidadSeries={CantidadSeries} reps={Reps} peso={Peso} unidad={Unidad} " +
            "tiempoSeg={TiempoSeg} descansoSeg={DescansoSeg} aclarar={CampoAclarar} " +
            "sugerencias=[{Sugerencias}]",
            message.MessageId,
            analisis.MensajeKind,
            analisis.TextoProcesado,
            analisis.Clasificacion.Intento,
            analisis.Clasificacion.Confianza?.ToString("0.00") ?? "-",
            analisis.Entidades?.EjercicioTexto ?? "-",
            analisis.ResolucionEjercicio?.Estado.ToString() ?? "-",
            analisis.ResolucionEjercicio?.EjercicioId,
            analisis.Entidades?.SerieOrdinal,
            analisis.Entidades?.CantidadSeries,
            analisis.Entidades?.Reps,
            analisis.Entidades?.PesoKg,
            analisis.Entidades?.Unidad.ToString() ?? "-",
            analisis.Entidades?.TiempoSegundos,
            analisis.Entidades?.DescansoSegundos,
            analisis.Entidades?.CampoAclarar ?? "-",
            Sugerencias(analisis.ResolucionEjercicio));

        return analisis;
    }

    private static string Sugerencias(ResultadoResolucionEjercicio? resolucion) =>
        resolucion is null || resolucion.Sugerencias is { Count: 0 }
            ? "-"
            : string.Join("; ", resolucion.Sugerencias.Select(s => s.NombreCanonico));
}
