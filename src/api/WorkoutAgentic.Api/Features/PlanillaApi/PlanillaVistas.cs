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

// ─── Catálogo para la cuadrícula ───────────────────────────────────────────────

// Proyección del catálogo para el selector de la SPA (agregar/editar fila).
public sealed record EjercicioCatalogoVista(
    int Id,
    string NombreCanonico,
    string GrupoMuscular,
    bool UsaPeso,
    bool UsaTiempo);

// ─── Comandos del CRUD de parciales (antes de confirmar) ──────────────────────

// POST /api/planillas/filas: fila NUEVA desde la cuadrícula; se exige fila
// completa según el tipo de ejercicio (el bot no está en el medio).
public sealed record ComandoNuevaFila(
    int AlumnoId,
    int EjercicioId,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg);

// PUT /api/planillas/filas/{filaId}: reemplazo completo de los campos. Con
// EjercicioId = null la fila queda como descripción libre (EjercicioCrudo).
public sealed record ComandoEdicionFila(
    int? EjercicioId,
    string? EjercicioCrudo,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg);

// POST /api/planillas/confirmar: abre el snapshot (write-once) cuando la
// sesión está en LISTA (regla de negocio §2).
public sealed record ComandoConfirmar(int AlumnoId);

// ─── Respuesta de confirmación (snapshot congelado) ──────────────────────────

public sealed record PlanillaConfirmadaVista(
    int Id,
    int AlumnoId,
    int SesionId,
    DateTime ConfirmadaUtc,
    List<FilaConfirmadaVista> Filas);

// Fila congelada tal como el profesor la va a ver (EjercicioCanonical es el
// nombre canónico, o el texto crudo en filas sin ejercicio).
public sealed record FilaConfirmadaVista(
    int Orden,
    string EjercicioCanonical,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg,
    bool Incompleta);
