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

    // Catálogo para el selector de la cuadrícula (agregar/editar filas).
    Task<List<EjercicioCatalogoVista>> CatalogoAsync(CancellationToken cancellationToken);
}

internal sealed class PlanillaConsulta(CapturasDbContext db) : IPlanillaConsulta
{
    public async Task<RespuestaSesionActual?> SesionActualAsync(int alumnoId, CancellationToken cancellationToken)
    {
        var alumno = await db.Alumnos
            .AsNoTracking()
            .Where(a => a.Id == alumnoId)
            .Select(a => new AlumnoVista(a.Id, a.Nombre, a.TelefonoWa))
            .SingleOrDefaultAsync(cancellationToken);
        if (alumno is null)
        {
            return null;
        }

        var sesion = await SesionHablanteSinTrackingAsync(alumnoId, cancellationToken);

        return VistaPlanilla.Armar(alumno.Id, alumno.Nombre, alumno.TelefonoWa, sesion);
    }

    // Catálogo para el selector de la cuadrícula (agregar/editar filas).
    public async Task<List<EjercicioCatalogoVista>> CatalogoAsync(CancellationToken cancellationToken) =>
        await db.Ejercicios
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new EjercicioCatalogoVista(e.Id, e.NombreCanonico, e.GrupoMuscular, e.UsaPeso, e.UsaTiempo))
            .ToListAsync(cancellationToken);

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

