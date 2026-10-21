namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Cola in-process (ADR-0002 S5): desacopla el ACK 200 del procesamiento del
// mensaje. Un solo Channel<T> en el MVP; la serialización por alumno (mensajes
// casi simultáneos del mismo teléfono) entra con el slice de SesionCaptura.
public interface IWhatsappMessageQueue
{
    void Write(IncomingWhatsappMessage message);

    IAsyncEnumerable<IncomingWhatsappMessage> ReadAllAsync(CancellationToken cancellationToken);
}
