using System.Threading.Channels;

namespace WorkoutAgentic.Api.Features.WhatsappSender;

// Implementación con Channel<T> (unbounded, un solo lector: el worker).
internal sealed class ChannelWhatsappOutboundQueue : IWhatsappOutboundQueue
{
    private readonly Channel<WhatsappOutboundText> _channel =
        Channel.CreateUnbounded<WhatsappOutboundText>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public void Enqueue(WhatsappOutboundText message)
    {
        if (!_channel.Writer.TryWrite(message))
        {
            // Unbounded nunca rechaza; si llegara a pasar es un bug interno.
            throw new InvalidOperationException("La cola de salida rechazó un mensaje inesperadamente.");
        }
    }

    public IAsyncEnumerable<WhatsappOutboundText> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
