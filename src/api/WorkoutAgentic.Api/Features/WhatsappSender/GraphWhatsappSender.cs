using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Envío de texto libre vía Graph API (docs/conventions/whatsapp.md):
// POST https://graph.facebook.com/{version}/{phone_number_id}/messages con
// Bearer. Sin secretos en logs; el teléfono va enmascarado. Los fallos de red,
// timeout, 5xx y 429 son Retriable=true; el resto de 4xx no se reintenta.
internal sealed class GraphWhatsappSender(
    HttpClient httpClient,
    IOptions<WhatsappOptions> options,
    ILogger<GraphWhatsappSender> logger) : IWhatsappSender
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Omitimos nulls para un payload compacto hacia Graph API (sin
        // "context":null cuando no hay reply).
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<WhatsappSendResult> SendTextAsync(WhatsappOutboundText message, CancellationToken cancellationToken)
    {
        var to = WhatsappPhone.Canonizar(message.To);
        if (to.Length == 0)
        {
            return FaltoTelefono();
        }

        var body = message.Body;
        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Envío descartado: cuerpo de texto vacío hacia {To}.", WhatsappPhone.Mask(to));
            return new WhatsappSendResult(
                Success: false, MessageId: null, ErrorCode: null,
                ErrorTitle: "Texto vacío", ErrorDetails: "El mensaje saliente no tiene cuerpo.", Retriable: false);
        }

        var (accessToken, phoneNumberId, apiVersion) = LeerConfiguracion();
        if (accessToken.Length == 0 || phoneNumberId.Length == 0 || apiVersion.Length == 0)
        {
            // Fail-closed: sin credenciales no se intenta (convención WhatsApp).
            logger.LogError(
                "Configuración de Graph API incompleta (AccessToken/PhoneNumberId/ApiVersion). Envío no realizado hacia {To}.",
                WhatsappPhone.Mask(to));
            return new WhatsappSendResult(
                Success: false, MessageId: null, ErrorCode: null,
                ErrorTitle: "Configuración incompleta",
                ErrorDetails: "Faltan WHATSAPP_ACCESS_TOKEN / WHATSAPP_PHONE_NUMBER_ID / WHATSAPP_API_VERSION.",
                Retriable: false);
        }

        var payload = new GraphOutboundPayload(
            MessagingProduct: "whatsapp",
            RecipientType: "individual",
            To: to,
            Type: "text",
            Text: new GraphOutboundTextPayload(Body: body),
            Context: message.ReplyToMessageId is { Length: > 0 } replyId
                ? new GraphOutboundContextPayload(MessageId: replyId)
                : null);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, PayloadJsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Cierre del host: no es un fallo de envío.
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Fallo de red contactando Graph API.");
            return new WhatsappSendResult(
                Success: false, MessageId: null, ErrorCode: null,
                ErrorTitle: "Fallo de red", ErrorDetails: "No se pudo contactar Graph API.", Retriable: true);
        }
        catch (OperationCanceledException)
        {
            // Timeout del HttpClient (el token externo no fue cancelado).
            logger.LogWarning("Timeout contactando Graph API.");
            return new WhatsappSendResult(
                Success: false, MessageId: null, ErrorCode: null,
                ErrorTitle: "Timeout", ErrorDetails: "Graph API no respondió a tiempo.", Retriable: true);
        }

        using (response)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Fallo(response.StatusCode, responseJson);
            }

            GraphOutboundResponse? okBody = null;
            try
            {
                okBody = JsonSerializer.Deserialize<GraphOutboundResponse>(responseJson, PayloadJsonOptions);
            }
            catch (JsonException)
            {
                // 2xx pero cuerpo no parseable: Meta aceptó el mensaje, no hay id.
            }

            var messageId = okBody?.Messages is { Count: > 0 } mensajes ? mensajes[0].Id : null;
            logger.LogInformation("Mensaje aceptado por Graph (id={MessageId}).", messageId ?? "sin-id");
            return WhatsappSendResult.Exito(messageId);
        }
    }

    private (string AccessToken, string PhoneNumberId, string ApiVersion) LeerConfiguracion()
    {
        var value = options.Value;
        return (value.AccessToken.Trim(), value.PhoneNumberId.Trim(), value.ApiVersion.Trim());
    }

    private WhatsappSendResult Fallo(HttpStatusCode statusCode, string responseJson)
    {
        GraphErrorDto? error = null;
        try
        {
            error = JsonSerializer.Deserialize<GraphErrorResponse>(responseJson, PayloadJsonOptions)?.Error;
        }
        catch (JsonException)
        {
            // Sin detalle parseable: se reporta el status HTTP.
        }

        var result = new WhatsappSendResult(
            Success: false,
            MessageId: null,
            ErrorCode: error?.Code,
            ErrorTitle: error?.Message ?? $"HTTP {(int)statusCode}",
            ErrorDetails: error?.ErrorData?.Details ?? Recortar(responseJson, 300),
            Retriable: statusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests);

        logger.LogWarning(
            "Graph API rechazó el envío: status={Status} code={Code} retriable={Retriable}.",
            (int)statusCode, result.ErrorCode?.ToString() ?? "-", result.Retriable);

        return result;
    }

    private static WhatsappSendResult FaltoTelefono() =>
        new(Success: false, MessageId: null, ErrorCode: null,
            ErrorTitle: "Teléfono inválido", ErrorDetails: "No hay dígitos en el teléfono de destino.", Retriable: false);

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..maximo] + "…";
}
