using Microsoft.Extensions.Logging;

namespace WorkoutAgentic.Api.Features.Nlu;

// Interpreta el texto final que el navegador envía después del reconocimiento
// por voz o de la escritura. No descarga ni procesa audio.
internal sealed class CapturaTextoNlu(
    IIntentClassifier classifier,
    IEntityExtractor extractor,
    ILogger<CapturaTextoNlu> logger) : IMensajesNlu
{
    public async Task<AnalisisCaptura?> AnalizarTextoAsync(
        string texto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            logger.LogWarning("Texto vacío en la entrada de análisis.");
            return null;
        }

        var limpio = texto.Trim();
        var clasificacion = await classifier.ClasificarAsync(limpio, cancellationToken);
        var entidades = await extractor.ExtraerAsync(limpio, cancellationToken);
        var analisis = new AnalisisCaptura(
            limpio,
            clasificacion,
            entidades.Exito ? entidades.Entidades : null,
            ResolucionEjercicio: null);

        logger.LogInformation(
            "Captura interpretada: texto='{Texto}' intent={Intento} (conf={Confianza}) " +
            "ejercicio='{Ejercicio}' normalizado='{EjercicioNormalizado}' serie={Serie} " +
            "cantidadSeries={CantidadSeries} reps={Reps} peso={Peso} unidad={Unidad} " +
            "tiempoSeg={TiempoSeg} descansoSeg={DescansoSeg} aclarar={CampoAclarar}",
            analisis.TextoProcesado,
            analisis.Clasificacion.Intento,
            analisis.Clasificacion.Confianza?.ToString("0.00") ?? "-",
            analisis.Entidades?.EjercicioTexto ?? "-",
            analisis.Entidades?.EjercicioNormalizado ?? "-",
            analisis.Entidades?.SerieOrdinal,
            analisis.Entidades?.CantidadSeries,
            analisis.Entidades?.Reps,
            analisis.Entidades?.PesoKg,
            analisis.Entidades?.Unidad.ToString() ?? "-",
            analisis.Entidades?.TiempoSegundos,
            analisis.Entidades?.DescansoSegundos,
            analisis.Entidades?.CampoAclarar ?? "-");

        return analisis;
    }
}
