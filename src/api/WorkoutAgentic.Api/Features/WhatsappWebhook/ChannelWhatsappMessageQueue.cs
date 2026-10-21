using System.Threading.Channels;

namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Channel<T> unbounded (ADR-0002 S5). SingleReader=true: hay un único
// CapturaWorker consumiendo en este slice.
internal sealed class ChannelWhatsappMessageQueue : IWhatsappMessageQueue
{
    private readonly Channel<IncomingWhatsappMessage> _channel =
        Channel.CreateUnbounded<IncomingWhatsappMessage>(
            new UnboundedChannelOptions { SingleReader = true });

    public void Write(IncomingWhatsappMessage message)
    {
        // Unbounded: TryWrite solo puede fallar si el canal quedó cerrado.
        if (!_channel.Writer.TryWrite(message))
        {
            throw new InvalidOperationException("La cola de captura está cerrada; no se pudo encolar el mensaje.");
        }
    }

    public IAsyncEnumerable<IncomingWhatsappMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
