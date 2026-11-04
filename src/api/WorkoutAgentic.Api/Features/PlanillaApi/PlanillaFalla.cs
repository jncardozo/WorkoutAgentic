namespace WorkoutAgentic.Api.Features.PlanillaApi;

using WorkoutAgentic.Api.Data.Entities;

// Fallas esperadas del dominio (convención: resultados esperados como tipos,
// nunca excepciones para lógica de negocio). El borde HTTP las convierte a
// ProblemDetails con el estado correspondiente.
internal sealed record PlanillaFalla(int EstadoHttp, string Titulo, string? Detalle = null)
{
    internal static PlanillaFalla AlumnoInexistente() => new(
        StatusCodes.Status404NotFound, "Alumno inexistente");

    internal static PlanillaFalla SesionInexistente() => new(
        StatusCodes.Status409Conflict,
        "Sin sesión activa de captura",
        "El alumno no tiene una sesión de captura abierta: empezá una captura desde la SPA.");

    internal static PlanillaFalla SesionSiendoAclarada() => new(
        StatusCodes.Status409Conflict,
        "Aclaración en curso",
        "Respondé o abandoná la aclaración desde la SPA antes de editar la cuadrícula.");

    internal static PlanillaFalla SesionExpirada() => new(
        StatusCodes.Status409Conflict,
        "Sesión pausada por inactividad",
        "Reanudá la carga desde la SPA antes de editar la planilla.");

    internal static PlanillaFalla FilaInexistente() => new(
        StatusCodes.Status404NotFound, "Fila inexistente");

    internal static PlanillaFalla FilaCongelada() => new(
        StatusCodes.Status409Conflict,
        "Fila de planilla confirmada",
        "La fila pertenece a una planilla ya confirmada y queda congelada: las correcciones van en una planilla nueva.");

    internal static PlanillaFalla EjercicioInexistente(int ejercicioId) => new(
        StatusCodes.Status400BadRequest,
        "Ejercicio inexistente en el catálogo",
        $"El ejercicio {ejercicioId} no está en el catálogo.");

    internal static PlanillaFalla DatosInvalidos(string detalle) => new(
        StatusCodes.Status400BadRequest, "Datos de serie inválidos", detalle);

    internal static PlanillaFalla SeriesPendientes(string detalle) => new(
        StatusCodes.Status409Conflict,
        "Completá el ejercicio actual",
        detalle);

    internal static PlanillaFalla ConfirmacionFueraDeEstado(EstadoSesionCaptura estadoActual) => new(
        StatusCodes.Status409Conflict,
        "La sesión no se puede confirmar ahora",
        $"La confirmación requiere una sesión en curso o lista para confirmar (estado actual: {estadoActual}). Reanudá la carga antes de intentarlo.");

    internal static PlanillaFalla ConfirmacionDePlanillaVacia() => new(
        StatusCodes.Status400BadRequest,
        "La planilla está vacía",
        "No hay filas cargadas en la sesión: confirmar necesita al menos una fila.");

    internal static PlanillaFalla ConfirmacionConAclaracionViva() => new(
        StatusCodes.Status409Conflict,
        "Aclaración pendiente en la sesión",
        "Respondé la aclaración pendiente antes de confirmar la planilla.");

    internal static PlanillaFalla SinSesionParaConfirmar() => new(
        StatusCodes.Status409Conflict,
        "No hay sesión para confirmar",
        "El alumno no tiene una sesión de captura abierta para confirmar.");
}
