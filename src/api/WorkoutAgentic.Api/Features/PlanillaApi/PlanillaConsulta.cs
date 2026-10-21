using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Lectura para la SPA (convención: AsNoTracking + proyección a DTO) del estado
// de la captura: sesión hablante del alumno + su cuadrícula de parciales.
// Nada muta acá — el diálogo de carga vive por WhatsApp (ADR-0004); este slice
// solo expone lo persistido para que el alumno lo revise y confirme.
internal interface IPlanillaConsulta
{
    // null = alumno inexistente (404 en el borde); Sesion = null sin sesión
    // hablante (sin sesiones o todas confirmadas).
    Task<RespuestaSesionActual?> SesionActualAsync(int alumnoId, CancellationToken cancellationToken);
}

internal sealed class PlanillaConsulta(CapturasDbContext db) : IPlanillaConsulta
{
    public async Task<RespuestaSesionActual?> SesionActualAsync(int alumnoId, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos
            .AsNoTracking()
            .Where(a => a.Id == alumnoId)
            .Select(a => new AlumnoVista(a.Id, a.TelefonoWa))
            .SingleOrDefaultAsync(cancellationToken);
        if (alumno is null)
        {
            return null;
        }

        var sesion = await db.SesionesCaptura
            .AsNoTracking()
            .Include(s => s.EjercicioActivo)
            .Include(s => s.AclaracionPendiente)
            .Include(s => s.Registros).ThenInclude(r => r.Ejercicio)
            .Where(s => s.AlumnoId == alumnoId
                // Sesión hablante: cualquier estado menos Confirmada (ADR-0004 S1).
                && s.Estado != EstadoSesionCaptura.Confirmada)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new RespuestaSesionActual(alumno, sesion is null ? null : MapearSesion(sesion));
    }

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
