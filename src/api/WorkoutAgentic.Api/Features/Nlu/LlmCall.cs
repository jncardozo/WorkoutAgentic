using System.Text;
using System.Text.Json;

namespace WorkoutAgentic.Api.Features.Nlu;

// Llamada compartida de los dos adaptadores de Gemini (STT y NLU): un solo
// shape de HTTP, errores binneados a string para los tipos de resultado.
internal static class LlmCall
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<(bool Exito, GeminiGenerateResponse? Respuesta, string? Error)> EnviarAsync(
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
            return (Exito: false, Respuesta: null, Error: $"Fallo de red: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return (Exito: false, Respuesta: null, Error: "Timeout del proveedor");
        }

        using (response)
        {
            var cuerpo = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string? mensaje = null;
                try
                {
                    mensaje = JsonSerializer.Deserialize<GeminiErrorResponse>(cuerpo, PayloadJsonOptions)?.Error is { } e
                        ? $"{e.Code} {e.Status}: {e.Message}"
                        : null;
                }
                catch (JsonException)
                {
                    // Sin detalle parseable: se reporta el status HTTP.
                }

                return (Exito: false, Respuesta: null,
                    Error: mensaje ?? $"HTTP {(int)response.StatusCode}: {Recortar(cuerpo, 200)}");
            }

            try
            {
                var respuesta = JsonSerializer.Deserialize<GeminiGenerateResponse>(cuerpo, PayloadJsonOptions);
                return (Exito: true, Respuesta: respuesta, Error: null);
            }
            catch (JsonException ex)
            {
                return (Exito: false, Respuesta: null, Error: $"Respuesta de Gemini no parseable: {ex.Message}");
            }
        }
    }

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..maximo] + "…";
}
