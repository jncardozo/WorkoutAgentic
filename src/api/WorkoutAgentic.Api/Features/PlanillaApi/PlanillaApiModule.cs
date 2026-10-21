using Microsoft.Extensions.DependencyInjection;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Slice PlanillaApi (arquitectura §2): vistas para la SPA del alumno. Esta parte
// es SOLO LECTURA: la edición de filas y la confirmación (snapshot POST) entran
// en slices siguientes — la carga sigue por WhatsApp (ADR-0004) y acá el alumno
// solo ve lo que el chat capturó. La identificación del alumno por ruta es
// provisional hasta AuthApi (arquitectura S6: cookie + roles por email).
internal static class PlanillaApiModule
{
    public static IServiceCollection AddPlanillaApi(this IServiceCollection services)
    {
        services.AddScoped<IPlanillaConsulta, PlanillaConsulta>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlanillaApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/planillas").WithTags("Planillas");

        group.MapGet("/sesion-actual/{alumnoId:int}", ObtenerSesionActual)
            .WithName("ObtenerSesionActual")
            .WithSummary("Sesión hablante del alumno con la cuadrícula de parciales");

        return endpoints;
    }

    // 404 SOLO si el alumno no existe. Sin sesión hablante responde 200 con
    // Sesion = null: es un estado normal de la cuenta, no un error.
    private static async Task<IResult> ObtenerSesionActual(
        int alumnoId,
        IPlanillaConsulta consulta,
        CancellationToken cancellationToken)
    {
        var respuesta = await consulta.SesionActualAsync(alumnoId, cancellationToken);
        return respuesta is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Alumno inexistente")
            : Results.Ok(respuesta);
    }
}
