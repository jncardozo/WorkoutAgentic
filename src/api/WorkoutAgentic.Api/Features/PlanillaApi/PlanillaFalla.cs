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
        "El alumno no tiene una sesión de captura abierta: se carga por WhatsApp y la planilla se abre con la primera carga.");

    internal static PlanillaFalla SesionSiendoAclarada() => new(
        StatusCodes.Status409Conflict,
        "Aclaración en curso por WhatsApp",
        "El bot está esperando una respuesta del alumno por WhatsApp: terminá o abandoná la aclaración ahí antes de editar la planilla.");

    internal static PlanillaFalla SesionExpirada() => new(
        StatusCodes.Status409Conflict,
        "Sesión pausada por inactividad",
        "Escribí \"seguir\" en el chat de WhatsApp para reanudar la carga antes de editar la planilla.");

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

    internal static PlanillaFalla ConfirmacionFueraDeEstado(EstadoSesionCaptura estadoActual) => new(
        StatusCodes.Status409Conflict,
        "La sesión no está lista para confirmar",
        $"La confirmación es solo para sesiones en Lista (estado actual: {estadoActual}). Mandá \"terminé\" por WhatsApp para cerrar la carga.");

    internal static PlanillaFalla ConfirmacionDePlanillaVacia() => new(
        StatusCodes.Status400BadRequest,
        "La planilla está vacía",
        "No hay filas cargadas en la sesión: confirmar necesita al menos una fila.");

    internal static PlanillaFalla ConfirmacionConAclaracionViva() => new(
        StatusCodes.Status409Conflict,
        "Aclaración pendiente en la sesión",
        "Resolvé o abandoná la aclaración por WhatsApp antes de confirmar.");

    internal static PlanillaFalla SinSesionParaConfirmar() => new(
        StatusCodes.Status409Conflict,
        "No hay sesión para confirmar",
        "El alumno no tiene la sesión en Lista: si ya se confirmó, la planilla quedó cerrada.");
}
