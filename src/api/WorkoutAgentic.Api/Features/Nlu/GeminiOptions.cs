namespace WorkoutAgentic.Api.Features.Nlu;

// Credenciales de Gemini API para interpretar el texto recibido desde la SPA.
// ApiKey NUNCA en appsettings.json: user-secrets en dev / env en prod.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    // GEMINI_API_KEY
    public string ApiKey { get; set; } = string.Empty;

    // Modelos flash VIGENTES para la key dev (probados 2026-10-23): la familia
    // `gemini-2.5-flash` quedó 404 «no longer available to new users» y el
    // free tier limita a 20 RPD POR MODELO (GenerateRequestsPerDayPerProjectPerModel-FreeTier).
    // · gemini-3-flash-preview: NLU correcto, pero su contador diario se
    //   agotó con las pruebas del día (reset a medianoche del Pacífico).
    // · gemini-3.8-flash: alta demanda (503) y >100 s pensando en el payload
    //   NLU real — inviable para NLU en free tier.
    // · gemini-3.6-flash: 503 persistente el día de la prueba.
    // · gemini-3.5-flash-lite: 200 en ~1 s y contador diario PROPIO (parche
    //   dev mientras el contador del 3-flash se recompone). Al ser lite puede
    //   omitir entidades: el pipeline deriva a aclaración, nunca inventa.
    // Facturación activada elimina el cap diario de cualquiera de estos.
    public string ModeloNlu { get; set; } = "gemini-3.5-flash-lite";

    // Cambiable para dev/proxy si hace falta; v1beta es la API pública.
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
