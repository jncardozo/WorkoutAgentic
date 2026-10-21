using Microsoft.Extensions.DependencyInjection;

namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Consumidor de la cola (BackgroundService, ADR-0002 S5). Por mensaje abre un
// scope y delega en el ICapturaPipeline que hayan registrado los slices
// posteriores (hoy: Features.Nlu). Sin pipeline registrado → log (deja los
// tests de integración del webhook sin dependencias de NLU).
internal sealed class CapturaWorker(
    IWhatsappMessageQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<CapturaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var pipeline = scope.ServiceProvider.GetService<ICapturaPipeline>();
                    if (pipeline is null)
                    {
                        logger.LogInformation(
                            "Mensaje sin pipeline registrado: id={MessageId} from={From} kind={Kind}",
                            message.MessageId, message.From, message.Kind);
                        continue;
                    }

                    await pipeline.ProcesarAsync(message, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw; // cierre del host: corta el bucle de cola.
                }
                catch (Exception ex)
                {
                    // El ACK 200 ya salió: un error interno no puede tumbar al
                    // worker ni reprocesar mensajes (docs/conventions/whatsapp.md).
                    logger.LogError(ex, "Error procesando mensaje id={MessageId}.", message.MessageId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre normal del host.
        }
    }
}
