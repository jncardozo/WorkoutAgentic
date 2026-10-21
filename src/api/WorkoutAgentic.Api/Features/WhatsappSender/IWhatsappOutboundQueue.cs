namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Cola de salida en proceso (S5 de ADR-0002): desacopla al productor (flujo de
// captura) del envío real (WhatsappDeliveryWorker). Al escalar multi-instancia
// se reemplaza por una cola externa → ADR futuro.
public interface IWhatsappOutboundQueue
{
    // No bloquea: la cola es unbounded y el worker la drena.
    void Enqueue(WhatsappOutboundText message);

    IAsyncEnumerable<WhatsappOutboundText> ReadAllAsync(CancellationToken cancellationToken);
}
