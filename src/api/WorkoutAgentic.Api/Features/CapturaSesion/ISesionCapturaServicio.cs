using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.WhatsappWebhook;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Etapa 4 del pipeline (spec NLU §5): aplica el análisis sobre el estado
// persistido de SesionCaptura y produce la respuesta del bot. Reglas de
// dominio en docs/domain/business-rules.md; decisiones del slice en ADR-0004.
internal interface ISesionCapturaServicio
{
    // Identifica/crea al alumno, abre o reanuda la sesión, resuelve aclaraciones
    // pendientes y aplica el intent analizado. Persiste TODO antes de devolver
    // la respuesta. El "ahora" lo pasa el pipeline (inyectable en pruebas).
    Task<CapturaRespuesta> AplicarAsync(
        IncomingWhatsappMessage mensaje,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken);

    // Entrada de texto directo desde la PWA (ADR-0009): el alumno ya viene
    // resuelto por id (el borde valida 404). Mismo núcleo de estado que el
    // flujo de WhatsApp, sin auditoría de audio.
    Task<CapturaRespuesta> AplicarTextoAsync(
        int alumnoId,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken);

    // Referencia el mensaje del bot (id de WhatsApp) dentro de la aclaración viva.
    Task MarcarMensajeBotAsync(int sesionId, string? mensajeBotWaId, CancellationToken cancellationToken);
}

// Respuesta lista para enviarse, más los datos que el pipeline necesita para
// la auditoría (AclaracionPendiente.MensajeBotWaId).
internal sealed record CapturaRespuesta(
    string MensajeAlumno,
    int SesionId,
    bool AclaracionCreada);
