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
    [property: JsonPropertyName("ejercicio_normalizado")] string? EjercicioNormalizado,
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
        "Sos el intérprete del dictado de entrenamientos del alumno en la app, en español " +
        "rioplatense. Tu única tarea es clasificar el mensaje y extraer entidades. " +
        "REGLA MÁS IMPORTANTE: nunca inventes datos; si un valor no está claro, poné null. " +
        "La carga del alumno va en DOS pasos sobre la misma cuadrícula.\n" +
        "No existe un catálogo cerrado de ejercicios. Reconocé el movimiento por su significado; " +
        "distintas formas equivalentes deben producir el mismo ejercicio_normalizado, por ejemplo " +
        "'press de banca con mancuernas' y 'banco plano con mancuernas' → 'Press de banca con mancuernas'. " +
        "No agregues implementos ni variantes que el alumno no haya dicho. Si no podés determinar " +
        "la variante con seguridad, usar aclarar.campo='ejercicio' y una pregunta abierta, sin opciones de catálogo.\n" +
        "PASO 1 · armar filas: en un mismo mensaje nombra PRIMERO el EJERCICIO y después la " +
        "CANTIDAD de series (\"press de banca 4 series\"), o el ejercicio con una serie puntual " +
        "(\"sillón, tercera serie\") → intent CARGA_EJERCICIO. Acá ejercicio SIEMPRE como texto " +
        "crudo tal como lo dijo en ejercicio, y en ejercicio_normalizado devolvé un nombre estándar " +
        "en español (p. ej. 'sillón' → 'Extensión de rodilla en máquina'); serie es el ordinal explícito 1..n " +
        "(\"primera\"→1), cantidad_series es el número de series dicho (\"4 series\"→4); " +
        "repeticiones, peso, unidad, tiempo_segundos y descanso_segundos SIEMPRE en null en este paso.\n" +
        "PASO 2 · cargar el dato de UNA serie ya armada, SIN nombrar ejercicio: NÚMERO de serie " +
        "(ordinal) + repeticiones y/o peso (\"primera serie 12 repeticiones con 20 kilos\", " +
        "\"segunda serie con 30\", \"tercera serie 10 al cuerpo\") → intent CARGA_SERIE. Acá " +
        "serie es el ordinal OBLIGATORIO (\"primera\"→1, \"segunda\"→2), extraé repeticiones " +
        "(\"12 repeticiones\"→12) y/o peso como número exacto (\"20 kilos\"→20, \"22 y " +
        "medio\"→22.5, \"40 en libras\" o \"40 libras\"→40 con unidad \"lb\"); unidad null si solo " +
        "dijo kilos o nada (el sistema asume kg), unidad \"peso_corporal\" si dijo \"al cuerpo\" " +
        "(con peso null). Ejercicios por tiempo: \"30 segundos\"→ tiempo_segundos solo si el " +
        "mensaje es claramente un dato de una serie; si no, null.\n" +
        "SIEMPRE: ejercicio null en el paso 2 (aunque el alumno hable de \"la serie del " +
        "prensa\", nunca lo extraigas ahí); si nombra ejercicio JUNTO a series, es PASO 1 y los " +
        "datos de serie van en null aunque los mencione (\"prensa 3 series de 10 con 20\" → solo " +
        "ejercicio + cantidad_series). Si en el paso 2 falta el ordinal (\"12 con 20\" sin " +
        "número de serie), igual extraé lo que haya con serie en null: el sistema responde con " +
        "la regla. No inventes ejercicios, series ni valores ausentes.\n" +
        "CIERRE son \"terminé\", \"listo\", \"ya está\", \"eso es todo\", \"no cargo más\". " +
        "AYUDA son consultas o comentarios que no cargan datos. INVALIDO saludos, spam u " +
        "otros temas ajenos al gym.\n" +
        "Usá aclarar solo para ambigüedad real del movimiento; nunca inventes ni enumeres opciones. " +
        "La confianza (0..1) refleja qué tan seguro estás del intent.";

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

        var (exito, respuesta, error, _) = await EnviarConReintentoTransitorioAsync(config, payload, cancellationToken);
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

    private const int SegundosReintento503 = 3;
    private const int CapSegundosReintento429 = 60;

    // Reintento ÚNICO de transporte ante fallos transitorios del proveedor:
    // el problema es de infraestructura, no del contenido, así que no viola la
    // regla de UNA llamada lógica del LLM por mensaje — el memo sigue
    // garantizando una sola interpretación por texto.
    // · 503 (alta demanda): espera fija de 3 s.
    // · 429 (cuota): SOLO si el propio proveedor anuncia RetryInfo con una
    //   ventana corta (≤60 s — típicamente el límite POR MINUTO del free tier,
    //   que se libera en el lapso que él mismo informa). Sin detalle o ventana
    //   larga (cuota diaria) NO reintenta: quemaría la cuota restante y
    //   demoraría sin esperanza de éxito.
    private async Task<(bool Exito, GeminiGenerateResponse? Respuesta, string? Error, int? StatusHttp)>
        EnviarConReintentoTransitorioAsync(
            GeminiOptions config, GeminiGenerateRequest payload, CancellationToken cancellationToken)
    {
        var primerIntento = await LlmCall.EnviarAsync(
            httpClient, config, config.ModeloNlu, payload, cancellationToken);

        if (primerIntento.Exito)
        {
            return (primerIntento.Exito, primerIntento.Respuesta, primerIntento.Error, primerIntento.StatusHttp);
        }

        if (primerIntento.StatusHttp == 429 && primerIntento.ReintentoTrasSegundos is { } espera429
            && espera429 <= CapSegundosReintento429)
        {
            logger.LogWarning(
                "429 de cuota (RESOURCE_EXHAUSTED); el proveedor pide esperar {Espera}s — reintentando una vez.",
                espera429);
            await Task.Delay(TimeSpan.FromSeconds(espera429), cancellationToken);

            var reenvio = await LlmCall.EnviarAsync(
                httpClient, config, config.ModeloNlu, payload, cancellationToken);
            return (reenvio.Exito, reenvio.Respuesta, reenvio.Error, reenvio.StatusHttp);
        }

        if (primerIntento.StatusHttp != 503)
        {
            return (primerIntento.Exito, primerIntento.Respuesta, primerIntento.Error, primerIntento.StatusHttp);
        }

        logger.LogWarning(
            "503 del proveedor (alta demanda); reintentando una vez en {Segundos}s.", SegundosReintento503);
        await Task.Delay(TimeSpan.FromSeconds(SegundosReintento503), cancellationToken);

        var intento503 = await LlmCall.EnviarAsync(
            httpClient, config, config.ModeloNlu, payload, cancellationToken);
        return (intento503.Exito, intento503.Respuesta, intento503.Error, intento503.StatusHttp);
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
        PreguntaAclarar: TrimVacio(datos.Aclarar?.Pregunta),
        EjercicioNormalizado: TrimVacio(datos.EjercicioNormalizado));

    private static string? TrimVacio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
}
