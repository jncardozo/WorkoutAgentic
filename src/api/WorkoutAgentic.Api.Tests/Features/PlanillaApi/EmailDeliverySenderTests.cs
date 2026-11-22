using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

public sealed class EmailDeliverySenderTests
{
    [Theory]
    [InlineData("pdf")]
    [InlineData("excel")]
    public async Task SendAsync_WithBrevo_SendsCorrectRecipientAndAttachmentOverHttps(string format)
    {
        using var provider = new RecordingProvider();
        var delivery = Sender(provider);
        Assert.True(delivery.IsConfigured);
        var plan = new PlanillaConfirmada
        {
            Id = 42, ConfirmadaUtc = DateTime.UtcNow,
            Filas = [new PlanillaFila { Orden = 1, EjercicioCanonical = "Sentadilla", NroSerie = 1, Reps = 10, PesoKg = 40 }],
        };
        var attachment = format == "pdf" ? PlanillaArchivoFactory.Pdf(plan) : PlanillaArchivoFactory.Excel(plan);
        var sender = new PlanillaCorreoSender(delivery);

        var result = await sender.EnviarAsync("qa@example.test", attachment, CancellationToken.None);

        Assert.Equal(EstadoEnvioCorreo.Enviado, result);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", provider.Url);
        Assert.Equal("test-api-key", provider.ApiKey);
        using var json = JsonDocument.Parse(provider.Body!);
        Assert.Equal("sender@example.test", json.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("qa@example.test", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        var sent = json.RootElement.GetProperty("attachment")[0];
        Assert.Equal(attachment.Nombre, sent.GetProperty("name").GetString());
        Assert.Equal(attachment.Contenido, Convert.FromBase64String(sent.GetProperty("content").GetString()!));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_WhenBrevoRejectsMessage_ReturnsFailureWithoutRetry(HttpStatusCode status)
    {
        using var provider = new RecordingProvider { Status = status };
        var result = await Sender(provider).SendAsync("qa@example.test", "Prueba", "Cuerpo", null, CancellationToken.None);
        Assert.Equal(EstadoEnvioCorreo.Fallo, result);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("brevo", "", "sender@example.test")]
    [InlineData("brevo", "test-key", "invalid")]
    [InlineData("unknown", "test-key", "sender@example.test")]
    public async Task SendAsync_WithMissingOrInvalidConfiguration_DoesNotCallProvider(string providerName, string key, string from)
    {
        using var provider = new RecordingProvider();
        var config = new EmailDeliveryOptions { Provider = providerName, ApiKey = key, FromAddress = from };
        var sender = Sender(provider, config);
        Assert.False(sender.IsConfigured);
        var result = await sender.SendAsync("qa@example.test", "Prueba", "Cuerpo", null, CancellationToken.None);
        Assert.Equal(EstadoEnvioCorreo.ConfiguracionIncompleta, result);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task SendAsync_WhenNetworkFails_ReturnsFailure()
    {
        using var provider = new RecordingProvider { Failure = new HttpRequestException("No network") };
        var result = await Sender(provider).SendAsync("qa@example.test", "Prueba", "Cuerpo", null, CancellationToken.None);
        Assert.Equal(EstadoEnvioCorreo.Fallo, result);
    }

    [Fact]
    public async Task SendAsync_WhenProviderTimesOut_ReturnsFailure()
    {
        using var provider = new RecordingProvider { Failure = new TaskCanceledException("Provider timeout") };
        var result = await Sender(provider).SendAsync("qa@example.test", "Prueba", "Cuerpo", null, CancellationToken.None);
        Assert.Equal(EstadoEnvioCorreo.Fallo, result);
    }

    [Fact]
    public async Task SendResetAsync_WithBrevo_UsesSameTransportWithoutAttachment()
    {
        using var provider = new RecordingProvider();
        var sender = new AccountEmailSender(Sender(provider), NullLogger<AccountEmailSender>.Instance);

        await sender.SendResetAsync("qa@example.test", "https://example.test/restablecer-clave?token=test-only", CancellationToken.None);

        using var json = JsonDocument.Parse(provider.Body!);
        Assert.Equal("qa@example.test", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Contains("token=test-only", json.RootElement.GetProperty("textContent").GetString());
        Assert.False(json.RootElement.TryGetProperty("attachment", out _));
    }

    private static EmailDeliverySender Sender(RecordingProvider provider, EmailDeliveryOptions? config = null) => new(
        Options.Create(new CorreoSmtpOptions()),
        Options.Create(config ?? new EmailDeliveryOptions { Provider = "brevo", ApiKey = "test-api-key", FromAddress = "sender@example.test" }),
        provider, NullLogger<EmailDeliverySender>.Instance);

    // Replaces the external provider at the HTTP boundary; no real credentials
    // or external email addresses are used by these contract tests.
    private sealed class RecordingProvider : HttpMessageHandler, IHttpClientFactory
    {
        public string? Body { get; private set; }
        public string? Url { get; private set; }
        public string? ApiKey { get; private set; }
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.Created;
        public Exception? Failure { get; init; }

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(EmailDeliverySender.ClientName, name);
            return new HttpClient(this, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Url = request.RequestUri!.ToString();
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Failure is not null) throw Failure;
            return new HttpResponseMessage(Status) { Content = new StringContent("{\"messageId\":\"test-message\"}") };
        }
    }
}
