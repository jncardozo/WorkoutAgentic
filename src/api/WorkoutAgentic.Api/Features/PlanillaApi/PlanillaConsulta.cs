using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Lectura para la SPA (convención: AsNoTracking + proyección a DTO) del estado
// de la captura: sesión hablante del alumno + su cuadrícula de parciales.
// Nada muta acá: expone el estado para que el alumno lo revise y confirme.
internal interface IPlanillaConsulta
{
    // null = alumno inexistente (404 en el borde); Sesion = null sin sesión
    // hablante (sin sesiones o todas confirmadas).
    Task<RespuestaSesionActual?> SesionActualAsync(int alumnoId, CancellationToken cancellationToken);

    // null = alumno inexistente; lista vacía = todavía no confirmó planillas.
    Task<List<PlanillaConfirmadaVista>?> HistorialAsync(int alumnoId, CancellationToken cancellationToken);

}

internal sealed class PlanillaConsulta(CapturasDbContext db) : IPlanillaConsulta
{
    public async Task<RespuestaSesionActual?> SesionActualAsync(int alumnoId, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos
            .AsNoTracking()
            .Where(a => a.Id == alumnoId)
            .Select(a => new AlumnoVista(a.Id, a.Nombre))
            .SingleOrDefaultAsync(cancellationToken);
        if (alumno is null)
        {
            return null;
        }

        var sesion = await SesionHablanteSinTrackingAsync(alumnoId, cancellationToken);

        return VistaPlanilla.Armar(alumno.Id, alumno.Nombre, sesion);
    }

    public async Task<List<PlanillaConfirmadaVista>?> HistorialAsync(
        int alumnoId,
        CancellationToken cancellationToken)
    {
        var existeAlumno = await db.Alumnos
            .AsNoTracking()
            .AnyAsync(a => a.Id == alumnoId, cancellationToken);
        if (!existeAlumno)
        {
            return null;
        }

        var planillas = await db.PlanillasConfirmadas
            .AsNoTracking()
            .Include(p => p.Filas)
            .Where(p => p.AlumnoId == alumnoId)
            .OrderByDescending(p => p.ConfirmadaUtc)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        return [.. planillas.Select(VistaPlanilla.MapearPlanillaConfirmada)];
    }

    // Sesión hablante con el grafo completo que necesita la vista (ADR-0004 S1:
    // la última del alumno con estado distinto de Confirmada).
    internal Task<SesionCaptura?> SesionHablanteSinTrackingAsync(int alumnoId, CancellationToken cancellationToken) =>
        db.SesionesCaptura
            .AsNoTracking()
            .Include(s => s.EjercicioActivo)
            .Include(s => s.AclaracionPendiente)
            .Include(s => s.Registros).ThenInclude(r => r.Ejercicio)
            .Where(s => s.AlumnoId == alumnoId
                && s.Estado != EstadoSesionCaptura.Confirmada)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

