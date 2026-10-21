namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Mensaje de texto saliente hacia el alumno (docs/conventions/whatsapp.md).
// Solo texto libre: las plantillas entran cuando el flujo de captura lo
// requiera (fuera de la ventana de 24 h). To llega canonizado desde
// TelefonoWa del alumno; el sender re-canoniza de forma defensiva.
public sealed record WhatsappOutboundText(
    string To,
    string Body,
    string? ReplyToMessageId = null);

// Resultado del envío: tipo de resultado del dominio, no excepciones
// (docs/conventions/dotnet.md). El worker decide reintento con Retriable.
public sealed record WhatsappSendResult(
    bool Success,
    string? MessageId,
    long? ErrorCode,
    string? ErrorTitle,
    string? ErrorDetails,
    bool Retriable)
{
    public static WhatsappSendResult Exito(string? messageId) =>
        new(Success: true, MessageId: messageId, ErrorCode: null, ErrorTitle: null, ErrorDetails: null, Retriable: false);
}
