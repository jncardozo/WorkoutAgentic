using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.Nlu;

// Analizador de mensajes del Voice Tracker (etapas 1→3 del pipeline):
//   texto:  NLU → resolver
//   audio:  descarga del medio → STT → NLU → resolver
// Devuelve null cuando una etapa falla (sin dato inventado). La aplicación de
// estado y las respuestas viven en Features.CapturaSesion (ICapturaPipeline).
public interface IMensajesNlu
{
    Task<AnalisisCaptura?> AnalizarAsync(IncomingWhatsappMessage message, CancellationToken cancellationToken);

    // Entrada de texto directo (PWA/dictado, ADR-0009): el mismo núcleo de
    // etapas 1→3 sin transporte de WhatsApp. Devuelve null si una etapa falla.
    Task<AnalisisCaptura?> AnalizarTextoAsync(
        string texto,
        string mensajeId,
        CancellationToken cancellationToken);
}
