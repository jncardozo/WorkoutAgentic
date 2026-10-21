namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Vista de una fila de la cuadrícula (Alumno · Planilla, arquitectura §6),
// proyectada desde RegistroParcial. Los faltantes reales van como null (regla
// madre: nunca inventar) y PendienteAclaracion marca las filas afectadas por
// una aclaración — viva (se responde por WhatsApp) o abandonada tras los
// 2 reintentos (ADR-0004 S4): en ambos casos son filas incompletas a mirar.
public sealed record FilaParcialVista(
    int Id,
    int? EjercicioId,
    string? EjercicioCanonico,
    string? EjercicioCrudo,
    bool DescripcionPendiente,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg,
    bool PendienteAclaracion,
    DateTime CreadoUtc);

// Pregunta viva del chat: la SPA solo la muestra (campo + reintento); la
// respuesta SIEMPRE llega por WhatsApp, nunca por la SPA.
public sealed record AclaracionVista(string Campo, int Reintento);

// Sesión hablante del alumno: la última con estado distinto de Confirmada
// (ADR-0004 S1). Estado y Unidad como texto canónico del enum — la vista los
// serializa a mano: el JSON queda estable aunque la config global cambie.
public sealed record SesionActualVista(
    int Id,
    string Estado,
    int? EjercicioActivoId,
    string? EjercicioActivoNombre,
    int OrdenSerieSiguiente,
    AclaracionVista? Aclaracion,
    DateTime UltimaActividadUtc,
    List<FilaParcialVista> Filas);

// GET /api/planillas/sesion-actual/{alumnoId}: Sesion = null cuando el alumno
// no tiene sesión hablante (sin sesiones o todas confirmadas) — es un estado
// normal, no un error (404 queda reservado a alumno inexistente).
public sealed record RespuestaSesionActual(AlumnoVista Alumno, SesionActualVista? Sesion);

// Encabezado mínimo de la cuenta. La identificación del alumno es provisional
// (id por ruta) hasta AuthApi (arquitectura S6: cookie + roles por email).
public sealed record AlumnoVista(int Id, string TelefonoWa);
