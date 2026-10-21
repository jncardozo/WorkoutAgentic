namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Tipos relevantes para la captura (docs/conventions/whatsapp.md §5): text y
// audio entran al pipeline; los demás van como Other (respuesta de cortesía).
public enum WhatsappMessageKind
{
    Text,
    Audio,
    Other,
}

// Mensaje normalizado a la altura de la cola. Slice 1 solo lo transporta:
// audio → descarga de medio → STT y texto → NLU entran en slices siguientes.
public sealed record IncomingWhatsappMessage(
    string MessageId,
    string From,
    WhatsappMessageKind Kind,
    string? TextBody,
    string? AudioMediaId,
    string? AudioMimeType,
    long? AudioDurationSeconds,
    string? AudioSha256);
