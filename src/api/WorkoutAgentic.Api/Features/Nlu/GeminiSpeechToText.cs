using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkoutAgentic.Api.Features.Nlu;

// STT por Gemini API: POST {base}/models/{modelo}:generateContent con la nota
// de voz en línea (base64) — ADR-0002 S1. Fallos → tipo de dominio; el flujo
// pide reenvío de la nota, no adivina. Nada del audio sale a los logs.
internal sealed class GeminiSpeechToText(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiSpeechToText> logger) : ISpeechToText
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Prompt fijo: transcripción literal, sin interpretar (la interpretación
    // es la etapa NLU, separada por diseño).
    private const string Prompt =
        "Transcribí esta nota de voz de una persona en un gimnasio. El idioma es español " +
        "rioplatense o hispanoamericano coloquial (se suelen nombrar ejercicios, series, " +
        "repeticiones y kilos). Respondé EXCLUSIVAMENTE con el texto transcribido, sin " +
        "comentar, corregir ni agregar nada.";

    public async Task<TranscripcionAudio> TranscribirAsync(
        byte[] audio, string mimeType, CancellationToken cancellationToken)
    {
        if (audio.Length == 0)
        {
            return TranscripcionAudio.Fallo("Audio vacío", "El archivo descargado no trae contenido.");
        }

        var config = options.Value;
        if (config.ApiKey.Trim().Length == 0)
        {
            logger.LogError("GEMINI_API_KEY no configurada: no se puede transcribir.");
            return TranscripcionAudio.Fallo(
                "Configuración incompleta", "Falta GEMINI_API_KEY (user-secrets / variables de entorno).");
        }

        var payload = new GeminiGenerateRequest(
            Contents:
            [
                new GeminiContent(
                [
                    new GeminiPart(Text: Prompt, InlineData: null),
                    new GeminiPart(Text: null, InlineData: new GeminiInlineData(
                        MimeType: mimeType,
                        Data: Convert.ToBase64String(audio))),
                ]),
            ],
            GenerationConfig: new GeminiGenerationConfig(Temperature: 0));

        var (exito, respuesta, error) = await LlmCall.EnviarAsync(
            httpClient, config, config.ModeloTranscripcion, payload, cancellationToken);
        if (!exito)
        {
            logger.LogWarning("Transcripción falló: {Error}.", error ?? "-");
            return TranscripcionAudio.Fallo(error ?? "Fallo de transcripción", null);
        }

        var texto = respuesta?.Candidates is { Count: > 0 } candidatos
            && candidatos[0].Content?.Parts is { Count: > 0 } partes
                ? string.Concat(partes.Select(p => p.Text ?? string.Empty))
                : null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            var bloqueado = respuesta?.PromptFeedback?.BlockReason;
            return TranscripcionAudio.Fallo(
                "Transcripción vacía",
                bloqueado is { Length: > 0 } ? $"Gemini no devolvió texto (motivo: {bloqueado})." : null);
        }

        return TranscripcionAudio.Ok(texto.Trim());
    }
}
