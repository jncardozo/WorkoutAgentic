using System.Text.Json.Serialization;

namespace WorkoutAgentic.Api.Features.Nlu;

// ─── Shape de generateContent (Gemini API REST) ───
internal sealed record GeminiGenerateRequest(
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent>? Contents,
    [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig? GenerationConfig);

internal sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart>? Parts);

internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("inline_data")] GeminiInlineData? InlineData);

// Nota de voz en línea (base64): para ≤ 2 min alcanza sin Files API.
internal sealed record GeminiInlineData(
    [property: JsonPropertyName("mime_type")] string MimeType,
    [property: JsonPropertyName("data")] string Data);

internal sealed record GeminiGenerationConfig(
    [property: JsonPropertyName("temperature")] double? Temperature = null,
    [property: JsonPropertyName("responseMimeType")] string? ResponseMimeType = null,
    [property: JsonPropertyName("responseSchema")] System.Text.Json.Nodes.JsonNode? ResponseSchema = null);

internal sealed record GeminiGenerateResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates,
    [property: JsonPropertyName("promptFeedback")] GeminiPromptFeedback? PromptFeedback);

internal sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason);

internal sealed record GeminiPromptFeedback(
    [property: JsonPropertyName("blockReason")] string? BlockReason);

// ─── Errores del endpoint ───
internal sealed record GeminiErrorResponse(
    [property: JsonPropertyName("error")] GeminiErrorDto? Error);

internal sealed record GeminiErrorDto(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("details")] IReadOnlyList<GeminiErrorDetail>? Details);

// Detalle google.rpc del error (interesa el RetryInfo de la cuota 429:
// anuncia cuántos segundos esperar antes de un reintento transitorio).
internal sealed record GeminiErrorDetail(
    [property: JsonPropertyName("@type")] string? Tipo,
    [property: JsonPropertyName("retryDelay")] string? RetryDelay);
