using System.Threading.Channels;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappWebhook;

// Doble de la cola: graba TODO lo escrito, aunque el worker (que corre en el
// host de tests) ya lo haya consumido. Permite afirmar el contenido encolado.
internal sealed class TestWhatsappMessageQueue : IWhatsappMessageQueue
{
    private readonly Channel<IncomingWhatsappMessage> _channel =
        Channel.CreateUnbounded<IncomingWhatsappMessage>();

    private readonly List<IncomingWhatsappMessage> _written = new();

    public IReadOnlyList<IncomingWhatsappMessage> Written => _written;

    public void Write(IncomingWhatsappMessage message)
    {
        _written.Add(message);
        _channel.Writer.TryWrite(message);
    }

    public IAsyncEnumerable<IncomingWhatsappMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
