using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Shape mínimo del payload de eventos de WhatsApp Cloud API
// (docs/conventions/whatsapp.md). El deserializador ignora los campos que este
// slice no necesita; los shape se agregan cuando los pida el slice
// correspondiente. No se asumen datos que no vengan en el payload.
public sealed record WebhookEnvelope(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] IReadOnlyList<WebhookEntry>? Entries);

public sealed record WebhookEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("changes")] IReadOnlyList<WebhookChange>? Changes);

public sealed record WebhookChange(
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("value")] WebhookChangeValue? Value);

public sealed record WebhookChangeValue(
    [property: JsonPropertyName("messaging_product")] string? MessagingProduct,
    [property: JsonPropertyName("contacts")] IReadOnlyList<WebhookContact>? Contacts,
    [property: JsonPropertyName("messages")] IReadOnlyList<WebhookPayloadMessage>? Messages,
    // Actualizaciones de entrega: se cuentan y se ACKea, sin procesar.
    [property: JsonPropertyName("statuses")] IReadOnlyList<JsonElement>? Statuses);

public sealed record WebhookContact(
    [property: JsonPropertyName("profile")] WebhookContactProfile? Profile,
    [property: JsonPropertyName("wa_id")] string? WaId);

public sealed record WebhookContactProfile([property: JsonPropertyName("name")] string? Name);

public sealed record WebhookPayloadMessage(
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WebhookMessageText? Text,
    [property: JsonPropertyName("audio")] WebhookMessageAudio? Audio);

public sealed record WebhookMessageText([property: JsonPropertyName("body")] string? Body);

// De la nota de voz se lleva solo la meta (media_id, mime, duración, sha256).
// NUNCA audio.link: es temporal y la convención prohíbe persistirla.
public sealed record WebhookMessageAudio(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("duration")] long? DurationSeconds,
    [property: JsonPropertyName("sha256")] string? Sha256);
