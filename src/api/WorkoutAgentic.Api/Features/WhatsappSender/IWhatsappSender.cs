namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Envío saliente por Graph API (docs/conventions/whatsapp.md). Implementación:
// GraphWhatsappSender. La decisión de ventana 24 h / plantilla la toma el flujo
// de captura (con Features.Whatsapp.WhatsappWindow), no el sender.
public interface IWhatsappSender
{
    // Nunca lanza por fallos de envío: devuelve WhatsappSendResult. Solo
    // propaga la cancelación del host (OperationCanceledException externa).
    Task<WhatsappSendResult> SendTextAsync(WhatsappOutboundText message, CancellationToken cancellationToken);
}
