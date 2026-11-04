using System.Text;
using System.Text.Json;

namespace WorkoutAgentic.Api.Features.Nlu;

// Llamada compartida de los adaptadores de Gemini para NLU: forma común de
// HTTP y errores normalizados para los tipos de resultado.
internal static class LlmCall
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // StatusHttp expone el código HTTP cuando hubo respuesta del servidor:
    // permite a los consumidores decidir reintentos transitorios sin acoplarse
    // al string de error (null en fallos de red/timeout, sin respuesta).
    // ReintentoTrasSegundos: solo en errores del servidor que anuncian
    // RetryInfo (cuota 429): la espera que el proveedor pide antes de
    // reintentar; null cuando no hay detalle parseable.
    public static async Task<(bool Exito, GeminiGenerateResponse? Respuesta, string? Error, int? StatusHttp, int? ReintentoTrasSegundos)> EnviarAsync(
        HttpClient httpClient,
        GeminiOptions config,
        string modelo,
        GeminiGenerateRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{config.BaseUrl.TrimEnd('/')}/models/{modelo}:generateContent")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, PayloadJsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("x-goog-api-key", config.ApiKey.Trim());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cierre del host.
        }
        catch (HttpRequestException ex)
        {
            return (Exito: false, Respuesta: null, Error: $"Fallo de red: {ex.Message}", StatusHttp: null, ReintentoTrasSegundos: null);
        }
        catch (OperationCanceledException)
        {
            return (Exito: false, Respuesta: null, Error: "Timeout del proveedor", StatusHttp: null, ReintentoTrasSegundos: null);
        }

        using (response)
        {
            var cuerpo = await response.Content.ReadAsStringAsync(cancellationToken);
            int? status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var error = JsonSerializer.Deserialize<GeminiErrorResponse>(cuerpo, PayloadJsonOptions)?.Error;
                    var mensaje = error is { } e
                        ? $"{e.Code} {e.Status}: {e.Message}"
                        : null;
                    return (Exito: false, Respuesta: null,
                        Error: mensaje ?? $"HTTP {(int)response.StatusCode}: {Recortar(cuerpo, 200)}",
                        StatusHttp: status,
                        ReintentoTrasSegundos: SegundosDeRetry(error?.Details));
                }
                catch (JsonException)
                {
                    // Sin detalle parseable: se reporta el status HTTP.
                    return (Exito: false, Respuesta: null,
                        Error: $"HTTP {(int)response.StatusCode}: {Recortar(cuerpo, 200)}",
                        StatusHttp: status, ReintentoTrasSegundos: null);
                }
            }

            try
            {
                var respuesta = JsonSerializer.Deserialize<GeminiGenerateResponse>(cuerpo, PayloadJsonOptions);
                return (Exito: true, Respuesta: respuesta, Error: null, StatusHttp: status, ReintentoTrasSegundos: null);
            }
            catch (JsonException ex)
            {
                return (Exito: false, Respuesta: null,
                    Error: $"Respuesta de Gemini no parseable: {ex.Message}", StatusHttp: status, ReintentoTrasSegundos: null);
            }
        }
    }

    // "retryDelay": "34s" del RetryInfo (cuota 429) → 34. Formatos no
    // numéricos o vacíos devuelven null (no se adivina).
    private static int? SegundosDeRetry(IReadOnlyList<GeminiErrorDetail>? detalles)
    {
        var crudo = detalles?
            .FirstOrDefault(d => d.Tipo?.EndsWith("/google.rpc.RetryInfo", StringComparison.OrdinalIgnoreCase) == true)
            ?.RetryDelay?.Trim();
        if (string.IsNullOrWhiteSpace(crudo))
        {
            return null;
        }

        var numero = crudo.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? crudo[..^1]
            : crudo;
        return int.TryParse(numero, out var segundos) && segundos > 0 ? segundos : null;
    }

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..maximo] + "…";
}
