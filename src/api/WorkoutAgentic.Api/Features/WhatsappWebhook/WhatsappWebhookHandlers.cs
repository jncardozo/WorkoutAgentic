using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Categoría de logs del slice (las clases static no pueden ser type args).
internal sealed class WhatsappWebhookLog;

// Handlers del webhook de WhatsApp Cloud API. El webhook SOLO orquesta
// (firma, ruteo y encolado): toda la lógica de negocio vive en otros slices
// (docs/conventions/dotnet.md).
internal static class WhatsappWebhookHandlers
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    // Handshake de verificación (docs/conventions/whatsapp.md): Meta envía
    // hub.mode=subscribe, hub.verify_token y hub.challenge; respondemos el
    // challenge en texto plano SOLO si el token coincide. Si no: 401 sin datos.
    public static IResult Verify(
        IOptions<WhatsappOptions> options,
        [FromQuery(Name = "hub.mode")] string? hubMode,
        [FromQuery(Name = "hub.verify_token")] string? hubVerifyToken,
        [FromQuery(Name = "hub.challenge")] string? hubChallenge)
    {
        var matches = string.Equals(hubMode, "subscribe", StringComparison.Ordinal)
                      && string.Equals(hubVerifyToken, options.Value.VerifyToken, StringComparison.Ordinal)
                      && !string.IsNullOrEmpty(hubChallenge);

        return matches
            ? Results.Text(hubChallenge, "text/plain")
            : Results.Unauthorized();
    }

    // Eventos (POST): la firma OBLIGATORIA se valida sobre el body crudo ANTES
    // de deserializar; después 200 inmediato y el procesamiento va en background
    // (si no ACKeamos, Meta reintenta con backoff: docs/conventions/whatsapp.md).
    public static async Task<IResult> Receive(
        HttpRequest request,
        IWhatsappMessageQueue queue,
        WhatsappSignatureValidator signatureValidator,
        ILogger<WhatsappWebhookLog> logger,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRawBodyAsync(request, cancellationToken);
        var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        if (!signatureValidator.IsValid(rawBody, signature))
        {
            // 401 y NO procesar (docs/conventions/whatsapp.md). El body jamás va
            // al log: puede contener contenido del alumno.
            logger.LogWarning("Webhook rechazado: firma X-Hub-Signature-256 inválida.");
            return Results.Unauthorized();
        }

        WebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawBody, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            // Payload firmado por Meta pero no parseable: se registra el error y
            // se ACKea igual, sin procesar nada (checklist de la skill).
            logger.LogError("Payload con firma válida y JSON inválido; se descarta.");
            return Results.Ok();
        }

        foreach (var entry in envelope?.Entries ?? [])
        {
            foreach (var change in entry.Changes ?? [])
            {
                var value = change.Value;
                if (value?.Messages is null)
                {
                    continue;
                }

                foreach (var message in value.Messages)
                {
                    var incoming = ToIncomingMessage(message);
                    queue.Write(incoming);
                    logger.LogInformation(
                        "Mensaje encolado: id={MessageId} from={From} kind={Kind} audioMediaId={AudioMediaId}",
                        incoming.MessageId, incoming.From, incoming.Kind, incoming.AudioMediaId ?? "-");
                }

                var statuses = value.Statuses?.Count ?? 0;
                if (statuses > 0)
                {
                    // Actualizaciones de entrega no afectan la captura: solo se registran.
                    logger.LogInformation("Actualización de entrega ignorada: {Count}.", statuses);
                }
            }
        }

        return Results.Ok();
    }

    // Lectura del body crudo ANTES de cualquier binding: la firma se valida
    // sobre los bytes que envió Meta, sin re-serializar
    // (la convención lo exige explícitamente).
    private static async Task<byte[]> ReadRawBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is { } length)
        {
            var buffer = new byte[length];
            await request.Body.ReadExactlyAsync(buffer, cancellationToken);
            return buffer;
        }

        // Sin Content-Length (chunked): se copia a memoria completa.
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static IncomingWhatsappMessage ToIncomingMessage(WebhookPayloadMessage message)
    {
        var kind = message.Type switch
        {
            "text" => WhatsappMessageKind.Text,
            "audio" => WhatsappMessageKind.Audio,
            _ => WhatsappMessageKind.Other,
        };

        return new IncomingWhatsappMessage(
            MessageId: message.Id ?? string.Empty,
            From: message.From ?? string.Empty,
            Kind: kind,
            TextBody: kind == WhatsappMessageKind.Text ? message.Text?.Body : null,
            AudioMediaId: kind == WhatsappMessageKind.Audio ? message.Audio?.Id : null,
            AudioMimeType: kind == WhatsappMessageKind.Audio ? message.Audio?.MimeType : null,
            AudioDurationSeconds: kind == WhatsappMessageKind.Audio ? message.Audio?.DurationSeconds : null,
            AudioSha256: kind == WhatsappMessageKind.Audio ? message.Audio?.Sha256 : null);
    }
}
