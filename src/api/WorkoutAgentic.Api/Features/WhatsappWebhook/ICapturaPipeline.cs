namespace WorkoutAgentic.Api.Features.WhatsappWebhook;

// Contrato de procesamiento de un mensaje ya encolado. El webhook SOLO firma,
// rutea y encola (docs/conventions/dotnet.md): los slices posteriores
// implementan este interfaz y CapturaWorker los invoca desde un scope por
// mensaje (hoy lo implementa Features.Nlu).
internal interface ICapturaPipeline
{
    Task ProcesarAsync(IncomingWhatsappMessage message, CancellationToken cancellationToken);
}
