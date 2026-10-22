namespace WorkoutAgentic.Api.Features.Nlu;

// Credenciales de Gemini API (ADR-0002 S1/S2). La MISMA API key hace la
// transcripción y el NLU (una integración para ambos, familia flash).
// ApiKey NUNCA en appsettings.json: user-secrets en dev / env en prod.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    // GEMINI_API_KEY
    public string ApiKey { get; set; } = string.Empty;

    // Modelos flash VIGENTES para la key dev (probados 2026-10-22): la familia
    // `gemini-2.5-flash` quedó 404 "no longer available to new users" y
    // `gemini-3.8-flash` / `flash-latest` alternaron 503 de alta demanda con el
    // payload grande del NLU. Los nombres se ajustan por config sin tocar código.
    public string ModeloTranscripcion { get; set; } = "gemini-3-flash-preview";

    public string ModeloNlu { get; set; } = "gemini-3-flash-preview";

    // Cambiable para dev/proxy si hace falta; v1beta es la API pública.
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
