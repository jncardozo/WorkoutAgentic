using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkoutAgentic.Api.Features.WhatsappSender;

// ─── Payload de envío a Graph API (docs/conventions/whatsapp.md) ───
internal sealed record GraphOutboundPayload(
    [property: JsonPropertyName("messaging_product")] string MessagingProduct,
    [property: JsonPropertyName("recipient_type")] string? RecipientType,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] GraphOutboundTextPayload? Text,
    [property: JsonPropertyName("context")] GraphOutboundContextPayload? Context);

internal sealed record GraphOutboundTextPayload(
    [property: JsonPropertyName("body")] string Body);

// Respuesta a un mensaje entrante del alumno (context.message_id).
internal sealed record GraphOutboundContextPayload(
    [property: JsonPropertyName("message_id")] string MessageId);

// ─── Respuesta exitosa de Graph ───
internal sealed record GraphOutboundResponse(
    [property: JsonPropertyName("contacts")] IReadOnlyList<JsonElement>? Contacts,
    [property: JsonPropertyName("messages")] IReadOnlyList<GraphOutboundDeliveryEntry>? Messages);

internal sealed record GraphOutboundDeliveryEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("message_status")] string? MessageStatus);

// ─── Respuesta de error de Graph ───
internal sealed record GraphErrorResponse(
    [property: JsonPropertyName("error")] GraphErrorDto? Error);

internal sealed record GraphErrorDto(
    [property: JsonPropertyName("code")] long? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("error_data")] GraphErrorDataDto? ErrorData);

internal sealed record GraphErrorDataDto(
    [property: JsonPropertyName("details")] string? Details);
