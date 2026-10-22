using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Features.Profesores;

// Vista de un alumno para las listas del profesor (ADR-0010): datos de cuenta
// + resumen de lo confirmado. TelefonoWa null = alumno sin WhatsApp registrado.
public sealed record AlumnoProfesorVista(
    int Id,
    string Nombre,
    string? TelefonoWa,
    int PlanillasConfirmadas,
    DateTime? UltimaConfirmadaUtc);

// Slice Profesor (arquitectura §7): SOLO LECTURA de planillas confirmadas
// (regla de roles; el profesor jamás toca sesiones ni parciales). Los
// endpoints son provisionales por identidad hasta AuthApi (S6): sin cookie,
// el grupo existe para acotar el contrato queAuth va a proteger.
internal static class ProfesoresModule
{
    public static IEndpointRouteBuilder MapProfesores(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/profesores").WithTags("Profesores");

        group.MapGet("/alumnos", ListarAlumnos)
            .WithName("ListarAlumnosDelProfesor")
            .WithSummary("Alumnos del profesor con el resumen de sus planillas confirmadas");

        group.MapGet("/alumnos/{alumnoId:int}/planillas", PlanillasDelAlumno)
            .WithName("PlanillasConfirmadasDelAlumno")
            .WithSummary("Todas las planillas confirmadas del alumno, la más nueva primero");

        return endpoints;
    }

    private static async Task<IResult> ListarAlumnos(
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        var alumnos = await db.Alumnos
            .AsNoTracking()
            .OrderBy(a => a.Nombre)
            .ThenBy(a => a.Id)
            .Select(a => new AlumnoProfesorVista(
                a.Id,
                a.Nombre,
                a.TelefonoWa,
                a.PlanillasConfirmadas.Count,
                (DateTime?)a.PlanillasConfirmadas.Max(p => p.ConfirmadaUtc)))
            .ToListAsync(cancellationToken);

        return Results.Ok(alumnos);
    }

    private static async Task<IResult> PlanillasDelAlumno(
        int alumnoId,
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        var existe = await db.Alumnos
            .AsNoTracking()
            .AnyAsync(a => a.Id == alumnoId, cancellationToken);
        if (!existe)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Alumno inexistente");
        }

        // MVP: volumen pequeño por alumno (cargas recientes) → entities cargadas
        // y mapeadas a mano, mismo shape del snapshot que ya consume la SPA.
        var planillas = await db.PlanillasConfirmadas
            .AsNoTracking()
            .Where(p => p.AlumnoId == alumnoId)
            .OrderByDescending(p => p.ConfirmadaUtc)
            .Include(p => p.Filas)
            .ToListAsync(cancellationToken);

        return Results.Ok(planillas.Select(MapearPlanilla).ToList());
    }

    private static PlanillaConfirmadaVista MapearPlanilla(PlanillaConfirmada planilla) => new(
        planilla.Id,
        planilla.AlumnoId,
        planilla.SesionId,
        planilla.ConfirmadaUtc,
        [.. planilla.Filas
            .OrderBy(f => f.Orden)
            .Select(MapearFila)]);

    private static FilaConfirmadaVista MapearFila(PlanillaFila fila) => new(
        fila.Orden,
        fila.EjercicioCanonical,
        fila.NroSerie,
        fila.Reps,
        fila.PesoKg,
        // Unidad como texto canónico del enum, igual que en la confirmación.
        fila.Unidad?.ToString(),
        fila.TiempoSeg,
        fila.Incompleta);
}
