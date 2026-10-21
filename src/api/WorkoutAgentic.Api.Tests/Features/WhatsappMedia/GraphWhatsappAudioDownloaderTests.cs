using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;
using WorkoutAgentic.Api.Features.WhatsappMedia;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappMedia;

// Descarga en 2 pasos (metadatos con url temporal + bytes) contra handler
// falso que enruta por URL: NUNCA llamadas reales a Meta (arquitectura §8).
public class GraphWhatsappAudioDownloaderTests
{
    private const string MetadataJson =
        """{"url":"https://lookaside.fbsbx.com/archivo.ogg","mime_type":"audio/ogg; codecs=opus","file_size":3}""";

    [Fact]
    public async Task Exito_BajaMetadatosYBytesConBearer()
    {
        var handler = new RecordingHandler(request =>
            request.RequestUri!.Host.Contains("graph.facebook.com")
                ? Json(MetadataJson)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([65, 66, 67]),
                });
        var downloader = Crear(handler);

        var archivo = await downloader.DescargarAsync("media-1", CancellationToken.None);

        Assert.True(archivo.Exito);
        Assert.Equal(new byte[] { 65, 66, 67 }, archivo.Bytes);
        Assert.Equal("audio/ogg; codecs=opus", archivo.MimeType);
        Assert.Equal(3, archivo.TamanoBytes);

        Assert.Equal(2, handler.Requests.Count); // metadatos + archivo

        var metadatos = handler.Requests[0];
        Assert.Equal("https://graph.facebook.com/v23.0/media-1", metadatos.RequestUri!.ToString());
        Assert.Equal("Bearer token-prueba", metadatos.Headers.Authorization!.ToString());

        var bytes = handler.Requests[1];
        Assert.Equal("https://lookaside.fbsbx.com/archivo.ogg", bytes.RequestUri!.ToString());
        Assert.Equal("Bearer token-prueba", bytes.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Metadatos400_MediaNoDisponibleNoRetriable()
    {
        var handler = new RecordingHandler(_ => Json("""{"error":{"code":631,"message":"not found"}}""", HttpStatusCode.BadRequest));
        var downloader = Crear(handler);

        var archivo = await downloader.DescargarAsync("media-roto", CancellationToken.None);

        Assert.False(archivo.Exito);
        Assert.False(archivo.Retriable);              // medio expirado: reintentar no sirve
        Assert.Single(handler.Requests);              // no se intenta bajar bytes
    }

    [Fact]
    public async Task Metadatos503_Retriable()
    {
        var handler = new RecordingHandler(_ => Json("{} ", HttpStatusCode.ServiceUnavailable));
        var downloader = Crear(handler);

        var archivo = await downloader.DescargarAsync("media-1", CancellationToken.None);

        Assert.False(archivo.Exito);
        Assert.True(archivo.Retriable);
    }

    [Fact]
    public async Task ArchivoDemasiadoGrande_FalloSinBajar()
    {
        const string grande = """{"url":"https://lookaside.fbsbx.com/gr.ogg","mime_type":"audio/ogg","file_size":30000000}""";
        var handler = new RecordingHandler(_ => Json(grande));
        var downloader = Crear(handler);

        var archivo = await downloader.DescargarAsync("media-1", CancellationToken.None);

        Assert.False(archivo.Exito);
        Assert.False(archivo.Retriable);
        Assert.Single(handler.Requests);              // corto antes del GET de bytes
    }

    [Fact]
    public async Task MediaIdVacio_FalloSinLlamarHttp()
    {
        var handler = new RecordingHandler(_ => Json(MetadataJson));
        var downloader = Crear(handler);

        var archivo = await downloader.DescargarAsync("   ", CancellationToken.None);

        Assert.False(archivo.Exito);
        Assert.Empty(handler.Requests);
    }

    private static GraphWhatsappAudioDownloader Crear(RecordingHandler handler)
    {
        var options = new WhatsappOptions
        {
            AccessToken = "token-prueba",
            ApiVersion = "v23.0",
        };

        return new GraphWhatsappAudioDownloader(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<GraphWhatsappAudioDownloader>.Instance);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
