namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Vista de una fila de la cuadrícula (Alumno · Planilla, arquitectura §6),
// proyectada desde RegistroParcial. Los faltantes reales van como null (regla
// madre: nunca inventar) y PendienteAclaracion marca las filas afectadas por
// una aclaración — viva (se responde desde la SPA) o abandonada tras los
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
    DateTime CreadoUtc,
    string? Comments = null,
    string? Observations = null,
    bool IsOneRepMax = false);

// Pregunta viva de captura, respondida desde la misma SPA.
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

// Encabezado mínimo del alumno vinculado a la identidad autenticada (ADR-0038).
public sealed record AlumnoVista(int Id, string Nombre);

// ─── Comandos del CRUD de parciales (antes de confirmar) ──────────────────────

// POST /api/planillas/filas: fila desde la cuadrícula, con nombre libre.
public sealed record ComandoNuevaFila(
    int AlumnoId,
    int? EjercicioId,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg,
    string? EjercicioNombre = null,
    string? Comments = null,
    string? Observations = null,
    bool IsOneRepMax = false);

// PUT /api/planillas/filas/{filaId}: reemplazo completo de los campos. Con
// EjercicioId = null la fila queda como descripción libre (EjercicioCrudo).
public sealed record ComandoEdicionFila(
    int? EjercicioId,
    string? EjercicioCrudo,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg,
    string? Comments = null,
    string? Observations = null,
    bool IsOneRepMax = false);

// POST /api/planillas/confirmar: abre el snapshot (write-once) cuando la
// sesión está en EnCurso o Lista (ADR-0017).
public sealed record ComandoConfirmar(int AlumnoId, bool AcceptIncomplete = false);

// ─── Respuesta de confirmación (snapshot congelado) ──────────────────────────

public sealed record PlanillaConfirmadaVista(
    int Id,
    int AlumnoId,
    int SesionId,
    DateTime ConfirmadaUtc,
    List<FilaConfirmadaVista> Filas,
    string? Name = null,
    List<OneRepMaxProgressView>? OneRepMaxProgress = null);

// Fila congelada de la planilla confirmada.
public sealed record FilaConfirmadaVista(
    int Orden,
    string EjercicioCanonical,
    int NroSerie,
    int? Reps,
    decimal? PesoKg,
    string? Unidad,
    int? TiempoSeg,
    bool Incompleta,
    string? Comments = null,
    string? Observations = null,
    bool IsOneRepMax = false);

public sealed record OneRepMaxProgressView(
    string Exercise, decimal? SessionMaxKg, decimal? ReferenceKg,
    DateTime? ReferenceConfirmedUtc, int? ReferencePlanillaId, int? ReferenceRowOrder,
    decimal? Percentage, string Description);
