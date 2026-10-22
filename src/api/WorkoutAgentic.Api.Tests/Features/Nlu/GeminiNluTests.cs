using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

// NLU contra handler falso: NUNCA llamadas reales a Gemini en tests
// (arquitectura §8). Verifica shape del request, parsing y el memo que
// garantiza UNA llamada del LLM para las dos etapas del mismo texto.
public class GeminiNluTests
{
    // helper: respuesta de generateContent con la salida JSON embebida en text.
    // El texto va codificado con JsonSerializer (escapa comillas/newlines: la
    // salida del LLM es JSON dentro de un string JSON).
    private static string Respuesta(string salidaJson) =>
        $$"""{"candidates":[{"content":{"parts":[{"text":{{JsonSerializer.Serialize(salidaJson)}}}]},"finishReason":"STOP"}]}""";

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Clasificar_PideSchemaYDevuelveIntent()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CARGA_SERIE","confianza":0.9,"serie":2,"repeticiones":10,"peso":40,"unidad":"kg"}""")));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("segunda serie, 10 con 40", CancellationToken.None);

        Assert.True(clasificacion.Exito, clasificacion.ErrorTitulo ?? "sin error");
        Assert.Equal(IntentoCaptura.CargaSerie, clasificacion.Intento);
        Assert.Equal(0.9, clasificacion.Confianza);

        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/models/gemini-2.5-flash:generateContent", request.RequestUri!.ToString());
        Assert.Equal("key-prueba", request.Headers.GetValues("x-goog-api-key").Single());
        var body = Assert.Single(handler.Bodies);
        Assert.Contains("\"responseMimeType\":\"application/json\"", body);
        Assert.Contains("\"CARGA_EJERCICIO\"", body);          // el schema los fuerza
        Assert.Contains("segunda serie, 10 con 40", body);    // el texto entra al prompt
    }

    [Fact]
    public async Task Extraer_MapeaEntidadesNormalizadas()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CARGA_EJERCICIO","confianza":0.8,"ejercicio":"sillón","serie":1,"repeticiones":12,"peso":45,"unidad":"kg"}""")));
        var nlu = Crear(handler);

        var entidades = (await nlu.ExtraerAsync("cargame sillón 12 con 45", CancellationToken.None)).Entidades;

        Assert.NotNull(entidades);
        Assert.Equal("sillón", entidades.EjercicioTexto);
        Assert.Equal(1, entidades.SerieOrdinal);
        Assert.Equal(12, entidades.Reps);
        Assert.Equal(45m, entidades.PesoKg);
        Assert.Equal(WorkoutAgentic.Api.Data.Unidad.Kg, entidades.Unidad);
        Assert.Null(entidades.CampoAclarar);
    }

    [Fact]
    public async Task DosEtapasMismoTexto_UnaSolaLlamadaAlLlm()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CARGA_SERIE","confianza":0.9,"repeticiones":10,"peso":40}""")));
        var nlu = Crear(handler);

        _ = await nlu.ClasificarAsync("hice 10 con 40", CancellationToken.None);
        _ = await nlu.ExtraerAsync("hice 10 con 40", CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TextoDistinto_VuelveALlamar()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CIERRE","confianza":0.5}""")));
        var nlu = Crear(handler);

        _ = await nlu.ClasificarAsync("ya está", CancellationToken.None);
        _ = await nlu.ClasificarAsync("eso es todo", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AclararSeMapeaAlEntidades()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CARGA_SERIE","confianza":0.4,"ejercicio":"remos","aclarar":{"campo":"peso","pregunta":"¿Con cuánto peso tomaste la serie?"}}""")));
        var nlu = Crear(handler);

        var entidades = (await nlu.ExtraerAsync("hice remos", CancellationToken.None)).Entidades;

        Assert.NotNull(entidades);
        Assert.Equal("peso", entidades.CampoAclarar);
        Assert.Equal("¿Con cuánto peso tomaste la serie?", entidades.PreguntaAclarar);
    }

    [Fact]
    public async Task UnidadTiempo_YConfianzaFueraDeRangoSeDescarta()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta(
            """{"intent":"CARGA_SERIE","confianza":1.5,"ejercicio":"plancha","unidad":"tiempo","tiempo_segundos":60}""")));
        var nlu = Crear(handler);

        var entidades = (await nlu.ExtraerAsync("plancha 1 minuto", CancellationToken.None)).Entidades;
        var clasificacion = await nlu.ClasificarAsync("plancha 1 minuto", CancellationToken.None);

        Assert.Equal(WorkoutAgentic.Api.Data.Unidad.Tiempo, entidades!.Unidad);
        Assert.Equal(60, entidades.TiempoSegundos);
        Assert.Equal(IntentoCaptura.CargaSerie, clasificacion.Intento);
        Assert.Null(clasificacion.Confianza); // 1.5 fuera de rango → null, no inventado
    }

    [Fact]
    public async Task IntentDesconocido_VuelveInvalido()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("""{"intent":"ALGO_RARO","confianza":0.2}""")));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("hola", CancellationToken.None);

        Assert.True(clasificacion.Exito);
        Assert.Equal(IntentoCaptura.Invalido, clasificacion.Intento);
    }

    [Fact]
    public async Task FalloDelProveedor_FallaEnAmbasEtapas()
    {
        var handler = new RecordingHandler(_ => Json(
            """{"error":{"code":429,"message":"RESOURCE_EXHAUSTED","status":"UNAVAILABLE"}}""",
            HttpStatusCode.TooManyRequests));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("hice 10", CancellationToken.None);
        var entidades = await nlu.ExtraerAsync("hice 10", CancellationToken.None);

        Assert.False(clasificacion.Exito);
        Assert.False(entidades.Exito);
        Assert.Contains("429", clasificacion.ErrorTitulo);
        Assert.Single(handler.Requests); // memo también cachea el fallo
    }

    [Fact]
    public async Task Transporte503_ReintentaUnaVez_YDaExito()
    {
        var llamadas = 0;
        var handler = new RecordingHandler(_ => ++llamadas == 1
            ? Json("{}", HttpStatusCode.ServiceUnavailable)
            : Json(Respuesta(
                """{"intent":"CARGA_SERIE","confianza":0.9,"repeticiones":10,"peso":40,"unidad":"kg"}""")));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("hice 10 con 40", CancellationToken.None);

        Assert.True(clasificacion.Exito, clasificacion.ErrorTitulo ?? "sin error");
        Assert.Equal(IntentoCaptura.CargaSerie, clasificacion.Intento);
        Assert.Equal(2, handler.Requests.Count); // 503 → un único reintento de transporte
    }

    [Fact]
    public async Task Transporte503_AgotadoElReintento_FallaSinTerceraLlamada()
    {
        var handler = new RecordingHandler(_ => Json("{}", HttpStatusCode.ServiceUnavailable));
        var nlu = Crear(handler);

        var entidades = await nlu.ExtraerAsync("prensa", CancellationToken.None);

        Assert.False(entidades.Exito);
        Assert.Contains("503", entidades.ErrorTitulo);
        Assert.Equal(2, handler.Requests.Count); // primer intento + reintento único, nada más
    }

    [Fact]
    public async Task SalidaVacia_FallaElPipeline()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("   ")));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("hola", CancellationToken.None);

        Assert.False(clasificacion.Exito);
        Assert.Equal("Interpretación vacía", clasificacion.ErrorTitulo);
    }

    [Fact]
    public async Task SalidaNoParseable_FallaElPipeline()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("esto-no-va-como-json {")));
        var nlu = Crear(handler);

        var clasificacion = await nlu.ClasificarAsync("hola", CancellationToken.None);

        Assert.False(clasificacion.Exito);
        Assert.Equal("Salida del LLM no parseable", clasificacion.ErrorTitulo);
    }

    [Fact]
    public async Task SinApiKey_FailClosedSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("""{"intent":null}""")));
        var nlu = Crear(handler, apiKey: string.Empty);

        var clasificacion = await nlu.ClasificarAsync("hola", CancellationToken.None);
        var entidades = await nlu.ExtraerAsync("hola", CancellationToken.None);

        Assert.False(clasificacion.Exito);
        Assert.False(entidades.Exito);
        Assert.Equal("Configuración incompleta", clasificacion.ErrorTitulo);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TextoVacio_FallasSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("""{"intent":null}""")));
        var nlu = Crear(handler);

        var entidades = await nlu.ExtraerAsync("   ", CancellationToken.None);

        Assert.False(entidades.Exito);
        Assert.Equal("Texto vacío", entidades.ErrorTitulo);
        Assert.Empty(handler.Requests);
    }

    private static GeminiNlu Crear(RecordingHandler handler, string? apiKey = "key-prueba")
    {
        var options = new GeminiOptions
        {
            ApiKey = apiKey ?? string.Empty,
            ModeloNlu = "gemini-2.5-flash",
        };

        return new GeminiNlu(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<GeminiNlu>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
    }
}
