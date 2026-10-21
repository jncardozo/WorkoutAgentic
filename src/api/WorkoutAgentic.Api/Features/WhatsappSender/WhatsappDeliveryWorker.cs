using Microsoft.Extensions.Logging;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.WhatsappSender;

// BackgroundService que drena la cola de salida y envía por Graph API,
// reintentando con backoff fijo (arquitectura §2: "cola de reintentos con
// backoff"). En el MVP la cola y este worker viven en-proceso: el webhook ya
// respondió 200, así que una pérdida aquí no rompe el ACK. El resultado de cada
// intento se registra; tras agotar el plan de reintentos el mensaje se descarta
// con log de error (dead-letter persistente → ADR futuro al escalar).
internal sealed class WhatsappDeliveryWorker(
    IWhatsappOutboundQueue queue,
    IWhatsappSender sender,
    ILogger<WhatsappDeliveryWorker> logger,
    IReadOnlyList<TimeSpan> retrySchedule) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                await EnviarConReintentosAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre normal del host.
        }
    }

    private async Task EnviarConReintentosAsync(WhatsappOutboundText message, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var result = await sender.SendTextAsync(message, cancellationToken);

            if (result.Success)
            {
                logger.LogInformation(
                    "Entrega ok a {To} en el intento {Intento} (id={MessageId}).",
                    WhatsappPhone.Mask(message.To), attempt + 1, result.MessageId ?? "sin-id");
                return;
            }

            if (!result.Retriable || attempt >= retrySchedule.Count)
            {
                logger.LogWarning(
                    "Envío descartado a {To} tras {Intentos} intento(s): code={Code} título={Titulo}.",
                    WhatsappPhone.Mask(message.To), attempt + 1,
                    result.ErrorCode?.ToString() ?? "-", result.ErrorTitle ?? "-");
                return;
            }

            var delay = retrySchedule[attempt];
            logger.LogWarning(
                "Intento {Intento} falló a {To} (code={Code}); reintento en {Delay}.",
                attempt + 1, WhatsappPhone.Mask(message.To),
                result.ErrorCode?.ToString() ?? "-", delay);
            await Task.Delay(delay, cancellationToken);
        }
    }
}
