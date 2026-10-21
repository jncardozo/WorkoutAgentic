using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappMedia;

// Descarga el audio de una nota de voz vía Graph API en 2 pasos:
//   1) GET https://graph.facebook.com/{v}/{media_id}  → JSON con url temporal.
//   2) GET esa url con Authorization Bearer → bytes del archivo.
// Fail-closed sin AccessToken; red/timeout/5xx/429 → Retriable. El audio.link
// NUNCA se persiste: solo bajan los bytes en memoria (convención WhatsApp).
internal sealed class GraphWhatsappAudioDownloader(
    HttpClient httpClient,
    IOptions<WhatsappOptions> options,
    ILogger<GraphWhatsappAudioDownloader> logger) : IWhatsappAudioDownloader
{
    // Límite defensivo: nota de voz real ≤ ~2 min; los inline base64 a Gemini
    // no deberían superar los 20 MB (ADR-0002 S1).
    private const long MaximoBytes = 20 * 1024 * 1024;

    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WhatsappAudioArchivo> DescargarAsync(string mediaId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return WhatsappAudioArchivo.Fallo("Media ID inválido", "El webhook no trajo media id.", retriable: false);
        }

        var config = options.Value;
        var accessToken = config.AccessToken.Trim();
        var apiVersion = config.ApiVersion.Trim();
        if (accessToken.Length == 0 || apiVersion.Length == 0)
        {
            logger.LogError("Configuración de Graph API incompleta: no se descarga el medio {MediaId}.", mediaId);
            return WhatsappAudioArchivo.Fallo(
                "Configuración incompleta",
                "Faltan WHATSAPP_ACCESS_TOKEN / WHATSAPP_API_VERSION.",
                retriable: false);
        }

        // Paso 1 — metadatos del medio (url temporal incluida).
        var metadata = await EnviarConResultado<GraphMediaMetadata>(
            $"https://graph.facebook.com/{apiVersion}/{Uri.EscapeDataString(mediaId)}",
            cancellationToken);
        if (!metadata.Exito || metadata.Datos is not { } meta)
        {
            return metadata.ErrorEsRetriable
                ? WhatsappAudioArchivo.Fallo("Fallo transitorio de Graph API", metadata.Error ?? "-", retriable: true)
                : WhatsappAudioArchivo.Fallo("Media no disponible", metadata.Error ?? "-", retriable: false);
        }

        if (meta.Url is not { Length: > 0 } url)
        {
            logger.LogWarning("Graph no devolvió url de descarga para el medio {MediaId}.", mediaId);
            return WhatsappAudioArchivo.Fallo("Media sin url", "Graph API no expuso la url del archivo.", retriable: false);
        }

        if (meta.FileSize is { } tamaño && tamaño > MaximoBytes)
        {
            return WhatsappAudioArchivo.Fallo(
                "Archivo demasiado grande",
                $"El medio pesa {tamaño} bytes y el límite del pipeline es {MaximoBytes}.",
                retriable: false);
        }

        // Paso 2 — bytes del archivo (la url temporal también exige Bearer).
        var bytes = await EnviarConResultado<byte[]>(url, cancellationToken);
        if (!bytes.Exito || bytes.Datos is not { } archivo)
        {
            return bytes.ErrorEsRetriable
                ? WhatsappAudioArchivo.Fallo("Fallo transitorio bajando el archivo", bytes.Error ?? "-", retriable: true)
                : WhatsappAudioArchivo.Fallo("No se pudo bajar el archivo", bytes.Error ?? "-", retriable: false);
        }

        var mime = meta.MimeType is { Length: > 0 } tipo ? tipo : "audio/ogg";
        return WhatsappAudioArchivo.Ok(archivo, mime, meta.FileSize ?? archivo.LongLength);
    }

    private async Task<(bool Exito, T? Datos, string? Error, bool ErrorEsRetriable)> EnviarConResultado<T>(
        string url, CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new("Bearer", options.Value.AccessToken.Trim());
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cierre del host.
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Fallo de red en Graph API (media).");
            return (Exito: false, Datos: null, Error: "Fallo de red", ErrorEsRetriable: true);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Timeout bajando desde Graph API (media).");
            return (Exito: false, Datos: null, Error: "Timeout", ErrorEsRetriable: true);
        }

        using (response)
        {
            if (typeof(T) == typeof(byte[]))
            {
                var contenido = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return response.IsSuccessStatusCode
                    ? (Exito: true, Datos: contenido as T, Error: null, ErrorEsRetriable: false)
                    : (Exito: false, Datos: null,
                        Error: $"HTTP {(int)response.StatusCode}", ErrorEsRetriable: response.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests);
            }

            var cuerpo = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var retriable = response.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests;
                return (Exito: false, Datos: null, Error: Recortar(cuerpo, 200), ErrorEsRetriable: retriable);
            }

            try
            {
                var datos = JsonSerializer.Deserialize<T>(cuerpo, MetadataJsonOptions);
                return (Exito: true, Datos: datos, Error: null, ErrorEsRetriable: false);
            }
            catch (JsonException)
            {
                return (Exito: false, Datos: null, Error: "Respuesta no parseable", ErrorEsRetriable: false);
            }
        }
    }

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..maximo] + "…";
}

// Metadatos del nodo media de Graph (el payload real trae más campos; estos
// DTOs solo toman lo que el slice necesita).
internal sealed record GraphMediaMetadata(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("file_size")] long? FileSize);
