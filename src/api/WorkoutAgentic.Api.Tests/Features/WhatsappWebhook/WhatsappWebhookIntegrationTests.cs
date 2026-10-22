using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappWebhook;

// Tests de integración con WebApplicationFactory y cola doble in-process:
// NUNCA se llama a Meta ni a proveedores externos (docs/conventions/dotnet.md).
public class WhatsappWebhookIntegrationTests : IDisposable
{
    private const string AppSecret = "integration-app-secret";
    private const string VerifyToken = "integration-verify-token";

    private const string TextEnvelopeJson =
        """{"object":"whatsapp_business_account","entry":[{"id":"WABA-ID","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","contacts":[{"profile":{"name":"Alumno Prueba"},"wa_id":"5215512345678"}],"messages":[{"from":"5215512345678","id":"wamid.TEST-1","type":"text","text":{"body":"cargame press de banca 12 con 80"}}]}}]}]}""";

    private const string AudioEnvelopeJson =
        """{"object":"whatsapp_business_account","entry":[{"id":"WABA-ID","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","messages":[{"from":"5215512345678","id":"wamid.TEST-2","type":"audio","audio":{"id":"media-1","mime_type":"audio/ogg; codecs=opus","duration":14,"sha256":"abcd"}}]}}]}]}""";

    private const string StatusesEnvelopeJson =
        """{"object":"whatsapp_business_account","entry":[{"id":"WABA-ID","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","statuses":[{"id":"wamid.TEST-3","status":"delivered"}]}]}]}]}""";

    private readonly TestWhatsappMessageQueue _queue = new();
    private readonly WebApplicationFactory<Program> _factory;

    // BD efímera por instancia de test: el DbInitializer del host corre
    // Migrate() sin pisar ninguna otra base en paralelo.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-hook-{Guid.NewGuid():N}.db");

    public WhatsappWebhookIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Secretos y BD para tests: UseSetting tiene más derecho que la
            // config de appsettings en hosts minimal-API (el connection string
            // se lee al construir el host, antes de los callbacks de config).
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            builder.UseSetting("Whatsapp:AppSecret", AppSecret);
            builder.UseSetting("Whatsapp:VerifyToken", VerifyToken);

            builder.ConfigureTestServices(services =>
                services.AddSingleton<IWhatsappMessageQueue>(_queue));

            // El webhook quedó deshabilitado en producción por el pivot (ADR-0009).
            // Esta suite enciende la bandera para seguir ejercitando firma,
            // handshake y cola del código que se conservó compilando.
            builder.UseSetting("Whatsapp:WebhookHabilitado", "true");
        });
    }

    public void Dispose()
    {
        _factory.Dispose();

        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // best effort: el archivo queda en %TEMP%.
                }
            }
        }
    }

    [Fact]
    public async Task Verify_WithValidHandshake_ReturnsChallengeAsPlainText()
    {
        var response = await Client().GetAsync(
            $"/api/whatsapp?hub.mode=subscribe&hub.verify_token={Uri.EscapeDataString(VerifyToken)}&hub.challenge=1234567890");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1234567890", await response.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_WithWrongVerifyToken_ReturnsUnauthorized()
    {
        var response = await Client().GetAsync(
            "/api/whatsapp?hub.mode=subscribe&hub.verify_token=token-incorrecto&hub.challenge=1234567890");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Receive_WithValidSignatureAndTextMessage_Acks200AndEnqueues()
    {
        var response = await PostSigned(TextEnvelopeJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(_queue.Written);
        Assert.Equal(WhatsappMessageKind.Text, message.Kind);
        Assert.Equal("cargame press de banca 12 con 80", message.TextBody);
        Assert.Equal("5215512345678", message.From);
        Assert.Equal("wamid.TEST-1", message.MessageId);
    }

    [Fact]
    public async Task Receive_WithValidSignatureAndAudioMessage_Acks200AndEnqueuesAudioMeta()
    {
        var response = await PostSigned(AudioEnvelopeJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(_queue.Written);
        Assert.Equal(WhatsappMessageKind.Audio, message.Kind);
        Assert.Equal("media-1", message.AudioMediaId);
        Assert.Equal("audio/ogg; codecs=opus", message.AudioMimeType);
        Assert.Equal((long?)14, message.AudioDurationSeconds);
        Assert.Equal("abcd", message.AudioSha256);
        Assert.Null(message.TextBody);
    }

    [Fact]
    public async Task Receive_WithInvalidSignature_Returns401AndDoesNotEnqueue()
    {
        // Firma de otro body: coincidiría con una clave válida pero no con estos bytes.
        var forgedDigest = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes("body-distinto"));
        var forgedHeader = "sha256=" + Convert.ToHexString(forgedDigest).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp")
        {
            Content = new StringContent(TextEnvelopeJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Hub-Signature-256", forgedHeader);

        var response = await Client().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_queue.Written);
    }

    [Fact]
    public async Task Receive_WithoutSignatureHeader_Returns401AndDoesNotEnqueue()
    {
        var response = await Client().PostAsync(
            "/api/whatsapp",
            new StringContent(TextEnvelopeJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_queue.Written);
    }

    [Fact]
    public async Task Receive_WithValidSignatureAndMalformedJson_Acks200AndSkipsProcessing()
    {
        var response = await PostSigned("esto-no-es-json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_queue.Written);
    }

    [Fact]
    public async Task Receive_WithStatusesOnlyPayload_Acks200WithoutEnqueue()
    {
        var response = await PostSigned(StatusesEnvelopeJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_queue.Written);
    }

    private HttpClient Client() => _factory.CreateClient();

    private async Task<HttpResponseMessage> PostSigned(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Hub-Signature-256", Signature(json));

        return await Client().SendAsync(request);
    }

    // La firma se calcula sobre los bytes EXACTOS del body que enviamos
    // (StringContent serializa UTF-8 sin trasformar el string).
    private static string Signature(string json) =>
        "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
}
