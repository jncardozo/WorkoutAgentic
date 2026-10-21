namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Consumidor de la cola (BackgroundService, ADR-0002 S5). Slice 1: registra el
// mensaje; acá se engancha el pipeline NLU (S2) y la descarga de medios en los
// slices siguientes.
internal sealed class CapturaWorker(
    IWhatsappMessageQueue queue,
    ILogger<CapturaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation(
                    "Mensaje listo para el pipeline: id={MessageId} from={From} kind={Kind}",
                    message.MessageId, message.From, message.Kind);
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre normal del host: no es un error.
        }
    }
}
