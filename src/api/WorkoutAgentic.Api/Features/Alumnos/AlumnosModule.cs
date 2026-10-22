using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;
using WorkoutAgentic.Api.Features.Whatsapp;

namespace WorkoutAgentic.Api.Features.Alumnos;

// Comando del borde del alta (ADR-0010): el id se asigna solo; el nombre es la
// etiqueta para las listas; el teléfono es OPCIONAL (identidad del canal
// deshabilitado) y nunca se inventa: ausente → null en la base.
public sealed record ComandoAltaAlumno(string Nombre, string? TelefonoWa);

// Slice Alumnos (ADR-0010): alta explícita en lugar del alta automática por
// primer contacto de WhatsApp (canal deshabilitado). Sin AuthApi (S6) los
// endpoints son provisionales: llegará el rol por cookie cuando exista.
internal static class AlumnosModule
{
    public static IEndpointRouteBuilder MapAlumnos(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/alumnos").WithTags("Alumnos");

        group.MapPost("/", DarDeAlta)
            .WithName("DarDeAltaAlumno")
            .WithSummary("Da de alta un alumno con nombre y teléfono opcional (ADR-0010)");

        return endpoints;
    }

    private static async Task<IResult> DarDeAlta(
        ComandoAltaAlumno comando,
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        // Canonización defensiva igual que el canal: solo dígitos.
        var telefono = WhatsappPhone.Canonizar(comando.TelefonoWa);
        if (telefono.Length > 0 && (telefono.Length < 6 || telefono.Length > 20))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Teléfono inválido",
                detail: "Dejanos el teléfono vacío o pasá el número internacional sin +, espacios ni guiones.");
        }

        var nombre = comando.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nombre requerido",
                detail: "Escribí el nombre del alumno: es la etiqueta de las listas del profesor.");
        }

        var alumno = new Alumno
        {
            Nombre = nombre,
            // Sin WhatsApp el teléfono falta de verdad: null, no un valor vacío
            // (el índice único trata los NULL por separado, igual que Email).
            TelefonoWa = telefono.Length > 0 ? telefono : null,
            Estado = "ACTIVO",
        };
        db.Alumnos.Add(alumno);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/alumnos/{alumno.Id}",
            new AlumnoVista(alumno.Id, alumno.Nombre, alumno.TelefonoWa));
    }
}
