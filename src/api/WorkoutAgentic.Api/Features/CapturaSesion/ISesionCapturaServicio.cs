using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

// Etapa 4 del pipeline (spec NLU §5): aplica el análisis sobre el estado
// persistido de SesionCaptura y produce la respuesta del bot. Reglas de
// dominio en docs/domain/business-rules.md; decisiones del slice en ADR-0004.
internal interface ISesionCapturaServicio
{
    // Identifica/crea al alumno, abre o reanuda la sesión, resuelve aclaraciones
    // pendientes y aplica el intent analizado. Persiste TODO antes de devolver
    // la respuesta. El "ahora" lo pasa el pipeline (inyectable en pruebas).
    // El texto escrito o reconocido en el navegador llega con el alumno
    // resuelto por id (el borde valida 404).
    Task<CapturaRespuesta> AplicarTextoAsync(
        int alumnoId,
        AnalisisCaptura analisis,
        DateTime ahora,
        CancellationToken cancellationToken);
}

// Respuesta de la captura que se muestra en la SPA.
internal sealed record CapturaRespuesta(
    string MensajeAlumno,
    int SesionId,
    bool AclaracionCreada);
