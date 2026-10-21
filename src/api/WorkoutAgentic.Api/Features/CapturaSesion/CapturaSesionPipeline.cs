using Microsoft.Extensions.Logging;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.WhatsappSender;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Único ICapturaPipeline registrado (el worker abre scope por mensaje):
// análisis (IMensajesNlu) → aplicar estado (ISesionCapturaServicio) →
// responder al alumno citando su mensaje original. Si una etapa falla no se
// envía nada con datos inventados; cada etapa deja su log de error propio.
internal sealed class CapturaSesionPipeline(
    IMensajesNlu mensajesNlu,
    ISesionCapturaServicio servicio,
    IWhatsappSender sender,
    ILogger<CapturaSesionPipeline> logger) : ICapturaPipeline
{
    public async Task ProcesarAsync(IncomingWhatsappMessage message, CancellationToken cancellationToken)
    {
        var analisis = await mensajesNlu.AnalizarAsync(message, cancellationToken);
        if (analisis is null)
        {
            return; // etapa de análisis fallida: sin datos para hablar del flujo
        }

        var respuesta = await servicio.AplicarAsync(message, analisis, DateTime.UtcNow, cancellationToken);

        var envio = await sender.SendTextAsync(
            new WhatsappOutboundText(message.From, respuesta.MensajeAlumno, ReplyToMessageId: message.MessageId),
            cancellationToken);

        if (!envio.Success)
        {
            logger.LogWarning(
                "Respuesta al mensaje {Msg} no enviada: titulo={Titulo} detalles={Detalles}.",
                message.MessageId, envio.ErrorTitle, envio.ErrorDetails);
            return;
        }

        logger.LogInformation("Respuesta enviada para el mensaje {Msg}: bot={BotId}.", message.MessageId, envio.MessageId);

        // La pregunta viva queda referenciada al mensaje del bot (auditoría).
        if (respuesta.AclaracionCreada)
        {
            await servicio.MarcarMensajeBotAsync(respuesta.SesionId, envio.MessageId, cancellationToken);
        }
    }
}
