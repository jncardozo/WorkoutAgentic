using Microsoft.Extensions.Logging.Abstractions;
using WorkoutAgentic.Api.Features.Whatsapp;
using WorkoutAgentic.Api.Features.WhatsappSender;

namespace WorkoutAgentic.Api.Tests.Features.WhatsappSender;

// WhatsappDeliveryWorker con StubWhatsappSender y plan de reintentos en cero
// (los delays reales —5 s/30 s/2 m— giran solo cuando DefaultRetrySchedule
// está registrado por el módulo; aquí se inyecta el plan para no esperar).
public class WhatsappDeliveryWorkerTests
{
    private const string Telefono = "5215512345678";

    [Fact]
    public async Task ExitoEnPrimerIntento_EnviaUnaVez()
    {
        var queue = new ChannelWhatsappOutboundQueue();
        var stub = new StubWhatsappSender(respuestaPorDefecto: WhatsappSendResult.Exito("wamid.1"));
        var worker = CrearWorker(queue, stub, [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]);

        await worker.StartAsync(CancellationToken.None);
        queue.Enqueue(new WhatsappOutboundText(Telefono, "hola"));

        await WaitUntil(() => stub.Sent.Count >= 1);
        await worker.StopAsync(CancellationToken.None);

        var sent = Assert.Single(stub.Sent);
        Assert.Equal(Telefono, sent.To);
        Assert.Equal("hola", sent.Body);
    }

    [Fact]
    public async Task FallosRetriable_ReintentaConBackoffHastaExito()
    {
        var queue = new ChannelWhatsappOutboundQueue();
        var stub = new StubWhatsappSender(resultadosEnCola:
        [
            FalloRetriable(131047),
            FalloRetriable(999),
            WhatsappSendResult.Exito("wamid.2"),
        ]);
        var worker = CrearWorker(queue, stub, [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]);

        await worker.StartAsync(CancellationToken.None);
        queue.Enqueue(new WhatsappOutboundText(Telefono, "hola"));

        await WaitUntil(() => stub.Sent.Count >= 3);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([Telefono, Telefono, Telefono], [.. stub.Sent.Select(sent => sent.To)]);
    }

    [Fact]
    public async Task FalloNoRetriable_SeDescartaSinReintentar()
    {
        var queue = new ChannelWhatsappOutboundQueue();
        var stub = new StubWhatsappSender(resultadosEnCola: [FalloNoRetriable(131026)]);
        var worker = CrearWorker(queue, stub, [TimeSpan.Zero, TimeSpan.Zero]);

        await worker.StartAsync(CancellationToken.None);
        queue.Enqueue(new WhatsappOutboundText(Telefono, "hola"));

        await WaitUntil(() => stub.Sent.Count >= 1);
        await worker.StopAsync(CancellationToken.None);

        var descartado = Assert.Single(stub.Sent);
        Assert.Equal(Telefono, descartado.To);
    }

    [Fact]
    public async Task PlanAgotado_SeDescartaTrasLenDelScheduleMasUnIntento()
    {
        var queue = new ChannelWhatsappOutboundQueue();
        var stub = new StubWhatsappSender(respuestaPorDefecto: FalloRetriable(131047));
        var worker = CrearWorker(queue, stub, [TimeSpan.Zero, TimeSpan.Zero]);

        await worker.StartAsync(CancellationToken.None);
        queue.Enqueue(new WhatsappOutboundText(Telefono, "hola"));

        await WaitUntil(() => stub.Sent.Count >= 3);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([Telefono, Telefono, Telefono], [.. stub.Sent.Select(sent => sent.To)]);
    }

    private static WhatsappDeliveryWorker CrearWorker(
        IWhatsappOutboundQueue queue,
        IWhatsappSender sender,
        IReadOnlyList<TimeSpan> schedule) =>
        new(queue, sender, NullLogger<WhatsappDeliveryWorker>.Instance, schedule);

    private static WhatsappSendResult FalloRetriable(long code) =>
        new(Success: false, MessageId: null, ErrorCode: code, ErrorTitle: "falla", ErrorDetails: null, Retriable: true);

    private static WhatsappSendResult FalloNoRetriable(long code) =>
        new(Success: false, MessageId: null, ErrorCode: code, ErrorTitle: "falla", ErrorDetails: null, Retriable: false);

    private static async Task WaitUntil(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException("El worker no llegó al estado esperado en 5 s.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class StubWhatsappSender(
        WhatsappSendResult? respuestaPorDefecto = null,
        params WhatsappSendResult[] resultadosEnCola) : IWhatsappSender
    {
        private readonly Queue<WhatsappSendResult> _results = new(resultadosEnCola);
        private readonly WhatsappSendResult _respuestaPorDefecto = respuestaPorDefecto ?? WhatsappSendResult.Exito(null);

        public List<WhatsappOutboundText> Sent { get; } = [];

        public Task<WhatsappSendResult> SendTextAsync(WhatsappOutboundText message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(_results.TryDequeue(out var result) ? result : _respuestaPorDefecto);
        }
    }
}
