namespace WorkoutAgentic.Api.Features.Nlu;

// Credenciales de Gemini API (ADR-0002 S1/S2). La MISMA API key hace la
// transcripción y el NLU (una integración para ambos, familia flash).
// ApiKey NUNCA en appsettings.json: user-secrets en dev / env en prod.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    // GEMINI_API_KEY
    public string ApiKey { get; set; } = string.Empty;

    // Modelo flash de la familia actual (ajustable sin tocar código).
    public string ModeloTranscripcion { get; set; } = "gemini-2.5-flash";

    public string ModeloNlu { get; set; } = "gemini-2.5-flash";

    // Cambiable para dev/proxy si hace falta; v1beta es la API pública.
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
