using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Features.Alumnos;

// Comando del alta explícita de alumno: el id se asigna solo.
public sealed record ComandoAltaAlumno(string Nombre);

// Slice Alumno: permite crear explícitamente la cuenta que usará la SPA.
internal static class AlumnosModule
{
    public static IEndpointRouteBuilder MapAlumnos(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/alumnos").WithTags("Alumnos");

        group.MapPost("/", DarDeAlta)
            .WithName("DarDeAltaAlumno")
            .WithSummary("Da de alta un alumno con nombre");

        return endpoints;
    }

    private static async Task<IResult> DarDeAlta(
        ComandoAltaAlumno comando,
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        var nombre = comando.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nombre requerido",
                detail: "Escribí el nombre del alumno.");
        }

        var alumno = new Alumno
        {
            Nombre = nombre,
            Estado = "ACTIVO",
        };
        db.Alumnos.Add(alumno);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/alumnos/{alumno.Id}",
            new AlumnoVista(alumno.Id, alumno.Nombre));
    }
}
