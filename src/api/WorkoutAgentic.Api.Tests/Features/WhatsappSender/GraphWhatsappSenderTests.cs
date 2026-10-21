using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;
using WorkoutAgentic.Api.Features.WhatsappSender;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappSender;

// GraphWhatsappSender contra un HttpMessageHandler falso: NUNCA llamadas reales
// a Meta en tests (arquitectura §8). El handler graba URL, Bearer y body.
public class GraphWhatsappSenderTests
{
    private const string RespuestaExitoJson =
        """{"messaging_product":"whatsapp","contacts":[{"input":"5215512345678","wa_id":"5215512345678"}],"messages":[{"id":"wamid.AbCdEf","message_status":"accepted"}]}""";

    [Fact]
    public async Task ExitoConUrlBearerYPayloadCorrectos()
    {
        var handler = new RecordingHandler(_ => Json(RespuestaExitoJson));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(
            new WhatsappOutboundText("+52 155 1234 5678", "Recibí tu entrenamiento"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("wamid.AbCdEf", result.MessageId);
        Assert.False(result.Retriable);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://graph.facebook.com/v23.0/NUMID123/messages", request.RequestUri!.ToString());
        Assert.Equal("Bearer token-test", request.Headers.Authorization!.ToString());
        Assert.Equal(HttpMethod.Post, request.Method);

        var body = Assert.Single(handler.Bodies);
        // Payload parseado estructuralmente (el serializador escapa no-ASCII,
        // así que Contains() contra la cadena cruda no es confiable).
        using var payload = JsonDocument.Parse(body);
        Assert.Equal("whatsapp", payload.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal("individual", payload.RootElement.GetProperty("recipient_type").GetString());
        // Convención: teléfono SIN +/espacios; el payload lleva canonizado.
        Assert.Equal("5215512345678", payload.RootElement.GetProperty("to").GetString());
        Assert.Equal("text", payload.RootElement.GetProperty("type").GetString());
        Assert.Equal("Recibí tu entrenamiento", payload.RootElement.GetProperty("text").GetProperty("body").GetString());
        // Sin mensaje al que responder → sin context en el payload.
        Assert.False(payload.RootElement.TryGetProperty("context", out _));
    }

    [Fact]
    public async Task ConReplyTo_IncluyeContextMessageId()
    {
        var handler = new RecordingHandler(_ => Json(RespuestaExitoJson));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(
            new WhatsappOutboundText("5215512345678", "hola", ReplyToMessageId: "wamid.REP"), CancellationToken.None);

        Assert.True(result.Success);
        var body = Assert.Single(handler.Bodies);
        using var payload = JsonDocument.Parse(body);
        Assert.Equal("wamid.REP", payload.RootElement.GetProperty("context").GetProperty("message_id").GetString());
    }

    [Fact]
    public async Task Error429RateLimit_EsRetriable()
    {
        const string json = """{"error":{"code":131047,"message":"Rate limit hit","error_data":{"details":"Reintenta más tarde"}}}""";
        var handler = new RecordingHandler(_ => Json(json, HttpStatusCode.TooManyRequests));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(131047L, result.ErrorCode);
        Assert.Equal("Rate limit hit", result.ErrorTitle);
        Assert.Equal("Reintenta más tarde", result.ErrorDetails);
        Assert.True(result.Retriable);
    }

    [Fact]
    public async Task Error500_EsRetriable()
    {
        var handler = new RecordingHandler(_ => Json("boom", HttpStatusCode.InternalServerError));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retriable);
        Assert.Equal("HTTP 500", result.ErrorTitle);
        Assert.Equal("boom", result.ErrorDetails);
    }

    [Fact]
    public async Task Error4xxNegocio_NoEsRetriable()
    {
        const string json = """{"error":{"code":131026,"message":"Message failed to send","error_data":{"details":"Revisa el pago de Meta"}}}""";
        var handler = new RecordingHandler(_ => Json(json, HttpStatusCode.BadRequest));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(131026L, result.ErrorCode);
        Assert.False(result.Retriable);
    }

    [Fact]
    public async Task Respuesta2xxNoParseable_ExitoSinMessageId()
    {
        var handler = new RecordingHandler(_ => Json("no-es-json"));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.MessageId);
    }

    [Fact]
    public async Task FalloDeRed_EsRetriable()
    {
        var handler = new ThrowingHandler(new HttpRequestException("dns"));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retriable);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task TelefonoInvalido_FallaSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(RespuestaExitoJson));
        var sender = CrearSender(handler);

        var result = await sender.SendTextAsync(new WhatsappOutboundText("   ", "hola"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retriable);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SinAccessToken_FailClosedSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(RespuestaExitoJson));
        var sender = CrearSender(handler, accessToken: string.Empty);

        var result = await sender.SendTextAsync(MensajeOk(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Configuración incompleta", result.ErrorTitle);
        Assert.False(result.Retriable);
        Assert.Empty(handler.Requests);
    }

    private static GraphWhatsappSender CrearSender(HttpMessageHandler handler, string? accessToken = "token-test")
    {
        var options = new WhatsappOptions
        {
            AppSecret = "app-secret",
            VerifyToken = "verify-token",
            AccessToken = accessToken ?? string.Empty,
            PhoneNumberId = "NUMID123",
            ApiVersion = "v23.0",
        };

        return new GraphWhatsappSender(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<GraphWhatsappSender>.Instance);
    }

    private static WhatsappOutboundText MensajeOk() => new("5215512345678", "hola");

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

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

    private sealed class ThrowingHandler(Exception excepcion) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(excepcion);
    }
}
