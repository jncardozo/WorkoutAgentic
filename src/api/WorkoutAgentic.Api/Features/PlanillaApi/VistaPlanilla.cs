using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Mapeo a las vistas de la SPA (compartido por consulta y mutaciones): las
// entidades del dominio viajan SOLO como proyección DTO acá, nunca crudas.
internal static class VistaPlanilla
{
    public static RespuestaSesionActual Armar(
        int alumnoId,
        string alumnoNombre,
        string? telefonoWa,
        SesionCaptura? sesion) => new(
        new AlumnoVista(alumnoId, alumnoNombre, telefonoWa),
        sesion is null ? null : MapearSesion(sesion));

    private static SesionActualVista MapearSesion(SesionCaptura sesion) => new(
        sesion.Id,
        sesion.Estado.ToString(),
        sesion.EjercicioActivoId,
        sesion.EjercicioActivo?.NombreCanonico,
        sesion.OrdenSerieSiguiente,
        sesion.AclaracionPendiente is { } aclaracion
            ? new AclaracionVista(aclaracion.Campo, aclaracion.Reintento)
            : null,
        sesion.UltimaActividadUtc,
        [.. sesion.Registros
            .OrderBy(r => r.Id)
            .Select(MapearFila)]);

    private static FilaParcialVista MapearFila(RegistroParcial fila) => new(
        fila.Id,
        fila.EjercicioId,
        fila.Ejercicio?.NombreCanonico,
        fila.EjercicioCrudo,
        fila.DescripcionPendiente,
        fila.NroSerie,
        fila.Reps,
        fila.PesoKg,
        fila.Unidad?.ToString(),
        fila.TiempoSeg,
        fila.PendienteAclaracion,
        fila.CreadoUtc);
}
