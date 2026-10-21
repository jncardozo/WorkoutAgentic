using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

// STT contra handler falso (sin llamadas reales a Gemini). Verifica shape del
// request (model, header de la key, inline_data base64) y parsing del texto.
public class GeminiSpeechToTextTests
{
    // El texto va codificado con JsonSerializer (comillas/newlines escapados).
    private static string Respuesta(string texto) =>
        $$"""{"candidates":[{"content":{"parts":[{"text":{{JsonSerializer.Serialize(texto)}}}]},"finishReason":"STOP"}]}""";

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Exito_DevuelveTextoTranscriptoRecortado()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("  hice remos con 50 y pico\n")));
        var stt = Crear(handler);

        var transcripcion = await stt.TranscribirAsync([65, 66, 67], "audio/ogg; codecs=opus", CancellationToken.None);

        Assert.True(transcripcion.Exito, transcripcion.ErrorTitulo ?? transcripcion.ErrorDetails ?? "sin error");
        Assert.Equal("hice remos con 50 y pico", transcripcion.Texto);

        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/models/gemini-2.5-flash:generateContent", request.RequestUri!.ToString());
        Assert.Equal("key-prueba", request.Headers.GetValues("x-goog-api-key").Single());

        var body = Assert.Single(handler.Bodies);
        Assert.Contains("\"inline_data\"", body);                       // audio en línea
        Assert.Contains("\"mime_type\":\"audio/ogg; codecs=opus\"", body);
        Assert.Contains("\"data\":\"QUJD\"", body);                     // base64 de ABC
        Assert.Contains("\"text\":\"Transcrib", body);                  // prompt fijo (ASCII-safe)
    }

    [Fact]
    public async Task ErrorDelProveedor_FalloTipado()
    {
        var handler = new RecordingHandler(_ => Json(
            """{"error":{"code":403,"message":"API key not valid","status":"PERMISSION_DENIED"}}""",
            HttpStatusCode.Forbidden));
        var stt = Crear(handler);

        var transcripcion = await stt.TranscribirAsync([1], "audio/ogg", CancellationToken.None);

        Assert.False(transcripcion.Exito);
        Assert.Contains("403", transcripcion.ErrorTitulo);
        Assert.Null(transcripcion.Texto);
    }

    [Fact]
    public async Task RespuestaSinTexto_FalloSinInventar()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("")));
        var stt = Crear(handler);

        var transcripcion = await stt.TranscribirAsync([1], "audio/ogg", CancellationToken.None);

        Assert.False(transcripcion.Exito);
        Assert.Equal("Transcripción vacía", transcripcion.ErrorTitulo);
        Assert.Null(transcripcion.Texto);
    }

    [Fact]
    public async Task AudioVacio_FalloSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("hola")));
        var stt = Crear(handler);

        var transcripcion = await stt.TranscribirAsync([], "audio/ogg", CancellationToken.None);

        Assert.False(transcripcion.Exito);
        Assert.Equal("Audio vacío", transcripcion.ErrorTitulo);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SinApiKey_FailClosedSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(Respuesta("hola")));
        var stt = Crear(handler, apiKey: "");

        var transcripcion = await stt.TranscribirAsync([1], "audio/ogg", CancellationToken.None);

        Assert.False(transcripcion.Exito);
        Assert.Equal("Configuración incompleta", transcripcion.ErrorTitulo);
        Assert.Empty(handler.Requests);
    }

    private static GeminiSpeechToText Crear(RecordingHandler handler, string? apiKey = "key-prueba")
    {
        var options = new GeminiOptions
        {
            ApiKey = apiKey ?? string.Empty,
            ModeloTranscripcion = "gemini-2.5-flash",
        };

        return new GeminiSpeechToText(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<GeminiSpeechToText>.Instance);
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
