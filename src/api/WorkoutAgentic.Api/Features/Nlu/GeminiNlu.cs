using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.Nlu;

// ─── Salida JSON del LLM (responseSchema de NluSchema) ───
internal sealed record AnalisisNluJson(
    [property: JsonPropertyName("intent")] string? Intent,
    [property: JsonPropertyName("confianza")] double? Confianza,
    [property: JsonPropertyName("ejercicio")] string? Ejercicio,
    [property: JsonPropertyName("serie")] int? Serie,
    [property: JsonPropertyName("cantidad_series")] int? CantidadSeries,
    [property: JsonPropertyName("repeticiones")] int? Repeticiones,
    [property: JsonPropertyName("peso")] decimal? Peso,
    [property: JsonPropertyName("unidad")] string? Unidad,
    [property: JsonPropertyName("tiempo_segundos")] int? TiempoSegundos,
    [property: JsonPropertyName("descanso_segundos")] int? DescansoSegundos,
    [property: JsonPropertyName("aclarar")] AclararNluJson? Aclarar);

internal sealed record AclararNluJson(
    [property: JsonPropertyName("campo")] string? Campo,
    [property: JsonPropertyName("pregunta")] string? Pregunta);

// Adaptador único de las etapas 2 y 3 sobre UNA llamada al LLM con
// response JSON Schema (ADR-0002 S2): una salida JSON auditable por mensaje.
// ClasificarAsync y ExtraerAsync sobre el MISMO texto comparten la llamada vía
// memo (la clase va scoped: un objeto por mensaje, sin estado cruzado).
internal sealed class GeminiNlu(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiNlu> logger) : IIntentClassifier, IEntityExtractor
{
    private const string Prompt =
        "Sos el intérprete de un bot que carga entrenamientos por WhatsApp en español " +
        "rioplatense. Tu única tarea es clasificar el mensaje y extraer entidades. " +
        "REGLA MÁS IMPORTANTE: nunca inventes datos. Si un valor falta o es ambiguo " +
        "por más de un criterio (\"y pico\", \"cincuenta y pico\", \"unos 40\"), poné null " +
        "en ese campo y completá aclarar.campo con qué preguntar.\n" +
        "Intents: CARGA_EJERCICIO nombra/actualiza un ejercicio (con o sin datos de serie; " +
        "si vienen datos de serie en el mismo mensaje sigue siendo CARGA_EJERCICIO). " +
        "CARGA_SERIE es datos de una serie SIN ejercicio nuevo (el activo de la sesión lo " +
        "provee). CIERRE son \"terminé\", \"listo\", \"ya está\", \"eso es todo\", \"no cargo más\". " +
        "AYUDA son consultas o comentarios que no cargan datos. INVALIDO saludos, spam u " +
        "otros temas ajenos al gym.\n" +
        "Campos: ejercicio SIEMPRE texto crudo tal como lo dijo (\"sillón\", no el nombre " +
        "canónico). serie es el ordinal explícito 1..n (\"primera\"→1). cantidad_series es " +
        "el multiplicador de \"3x12\"→3. peso en kg: \"45\", \"45 kilos\", \"45 y medio\"→45.5; " +
        "solo si dice otra unidad (\"en libras\") usar unidad. \"al cuerpo\"→unidad " +
        "peso_corporal y peso null. Tiempos: \"1 minuto de plancha\"→unidad tiempo y " +
        "tiempo_segundos 60. descanso_segundos solo si lo menciona. La confianza " +
        "(0..1) refleja qué tan seguro estás del intent.";

    private (string Texto, bool Exito, AnalisisNluJson? Datos, string? ErrorTitulo)? _memo;

    public async Task<ClasificacionIntent> ClasificarAsync(string texto, CancellationToken cancellationToken)
    {
        var analisis = await AnalizarAsync(texto, cancellationToken);
        return analisis.Exito
            ? ClasificacionIntent.Ok(ParseIntento(analisis.Datos!.Intent), analisis.Datos.Confianza)
            : ClasificacionIntent.Fallo(analisis.ErrorTitulo ?? "Fallo del LLM");
    }

    public async Task<EntidadesExtraidas> ExtraerAsync(string texto, CancellationToken cancellationToken)
    {
        var analisis = await AnalizarAsync(texto, cancellationToken);
        return analisis.Exito
            ? EntidadesExtraidas.Ok(ADtoEntidades(analisis.Datos!))
            : EntidadesExtraidas.Fallo(analisis.ErrorTitulo ?? "Fallo del LLM");
    }

    private async Task<(bool Exito, AnalisisNluJson? Datos, string? ErrorTitulo)> AnalizarAsync(
        string texto, CancellationToken cancellationToken)
    {
        if (_memo is { } memo && memo.Texto == texto)
        {
            return (memo.Exito, memo.Datos, memo.ErrorTitulo);
        }

        var resultado = await LlamarAsync(texto, cancellationToken);
        _memo = (texto, resultado.Exito, resultado.Datos, resultado.ErrorTitulo);
        return resultado;
    }

    private async Task<(bool Exito, AnalisisNluJson? Datos, string? ErrorTitulo)> LlamarAsync(
        string texto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return (Exito: false, Datos: null, ErrorTitulo: "Texto vacío");
        }

        var config = options.Value;
        if (config.ApiKey.Trim().Length == 0)
        {
            logger.LogError("GEMINI_API_KEY no configurada: no hay interpretación NLU.");
            return (Exito: false, Datos: null, ErrorTitulo: "Configuración incompleta");
        }

        var payload = new GeminiGenerateRequest(
            Contents:
            [
                new GeminiContent(
                [
                    new GeminiPart(Text: Prompt, InlineData: null),
                    new GeminiPart(Text: $"\n\nMENSAJE: {texto}", InlineData: null),
                ]),
            ],
            GenerationConfig: new GeminiGenerationConfig(
                Temperature: 0,
                ResponseMimeType: "application/json",
                ResponseSchema: NluSchema.ComoJson()));

        var (exito, respuesta, error) = await LlmCall.EnviarAsync(
            httpClient, config, config.ModeloNlu, payload, cancellationToken);
        if (!exito)
        {
            logger.LogWarning("NLU falló: {Error}.", error ?? "-");
            return (Exito: false, Datos: null, ErrorTitulo: error ?? "Fallo del LLM");
        }

        var salida = respuesta?.Candidates is { Count: > 0 } candidatos
            && candidatos[0].Content?.Parts is { Count: > 0 } partes
                ? string.Concat(partes.Select(p => p.Text ?? string.Empty))
                : null;

        if (string.IsNullOrWhiteSpace(salida))
        {
            var bloqueado = respuesta?.PromptFeedback?.BlockReason;
            return (Exito: false, Datos: null, ErrorTitulo: bloqueado is { Length: > 0 }
                ? $"Gemini no devolvió interpretación (motivo: {bloqueado})."
                : "Interpretación vacía");
        }

        try
        {
            var datos = JsonSerializer.Deserialize<AnalisisNluJson>(salida, PayloadJsonOptions);
            if (datos is null)
            {
                return (Exito: false, Datos: null, ErrorTitulo: "Interpretación vacía");
            }

            if (datos.Confianza is < 0 or > 1)
            {
                logger.LogWarning("Confianza fuera de rango ({Confianza}); se la descarta.", datos.Confianza);
                datos = datos with { Confianza = null };
            }

            return (Exito: true, Datos: datos, ErrorTitulo: null);
        }
        catch (JsonException)
        {
            return (Exito: false, Datos: null, ErrorTitulo: "Salida del LLM no parseable");
        }
    }

    private static IntentoCaptura ParseIntento(string? intent) => intent switch
    {
        "CARGA_EJERCICIO" => IntentoCaptura.CargaEjercicio,
        "CARGA_SERIE" => IntentoCaptura.CargaSerie,
        "CIERRE" => IntentoCaptura.Cierre,
        "AYUDA" => IntentoCaptura.AyudaConsulta,
        _ => IntentoCaptura.Invalido, // incluye null/valor fuera del enum: no se inventa.
    };

    private static Unidad? ParseUnidad(string? unidad) => unidad switch
    {
        "kg" => Unidad.Kg,
        "lb" => Unidad.Lb,
        "peso_corporal" => Unidad.PesoCorporal,
        "tiempo" => Unidad.Tiempo,
        _ => null,
    };

    private static EntidadesNlu ADtoEntidades(AnalisisNluJson datos) => new(
        EjercicioTexto: TrimVacio(datos.Ejercicio),
        SerieOrdinal: datos.Serie,
        CantidadSeries: datos.CantidadSeries,
        Reps: datos.Repeticiones,
        PesoKg: datos.Peso,
        Unidad: ParseUnidad(datos.Unidad),
        TiempoSegundos: datos.TiempoSegundos,
        DescansoSegundos: datos.DescansoSegundos,
        CampoAclarar: TrimVacio(datos.Aclarar?.Campo),
        PreguntaAclarar: TrimVacio(datos.Aclarar?.Pregunta));

    private static string? TrimVacio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
}
