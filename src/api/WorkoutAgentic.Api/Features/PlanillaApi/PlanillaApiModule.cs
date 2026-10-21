using Microsoft.Extensions.DependencyInjection;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// Slice PlanillaApi (arquitectura §2): vistas y CRUD de parciales para la SPA
// del alumno + confirmación (snapshot). La carga NUEVA sigue por WhatsApp
// (ADR-0004); acá el alumno revisa y corrige lo capturado, y confirma cuando
// la sesión está en Lista. La identificación del alumno por id es provisional
// hasta AuthApi (arquitectura S6: cookie + roles por email).
internal static class PlanillaApiModule
{
    public static IServiceCollection AddPlanillaApi(this IServiceCollection services)
    {
        services.AddScoped<IPlanillaConsulta, PlanillaConsulta>();
        services.AddScoped<IPlanillaMutaciones, PlanillaMutaciones>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlanillaApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/planillas").WithTags("Planillas");

        group.MapGet("/sesion-actual/{alumnoId:int}", ObtenerSesionActual)
            .WithName("ObtenerSesionActual")
            .WithSummary("Sesión hablante del alumno con la cuadrícula de parciales");

        group.MapGet("/catalogo-de-ejercicios", async (IPlanillaConsulta consulta, CancellationToken cancellationToken) =>
            {
                var catalogo = await consulta.CatalogoAsync(cancellationToken);
                return Results.Ok(catalogo);
            })
            .WithName("ObtenerCatalogoEjercicios")
            .WithSummary("Catálogo de ejercicios para el selector de la cuadrícula");

        // ─── CRUD de parciales (solo sesión en EnCurso o Lista) ───────────────

        group.MapPost("/filas", async (ComandoNuevaFila comando, IPlanillaMutaciones mutaciones, CancellationToken cancellationToken) =>
            {
                var resultado = await mutaciones.AgregarFilaAsync(comando.AlumnoId, comando, cancellationToken);
                return ARespuesta(resultado);
            })
            .WithName("AgregarFilaParcial")
            .WithSummary("Agrega una fila completa a la cuadrícula de la sesión hablante");

        group.MapPut("/filas/{filaId:int}", async (int filaId, ComandoEdicionFila comando, IPlanillaMutaciones mutaciones, CancellationToken cancellationToken) =>
            {
                var resultado = await mutaciones.EditarFilaAsync(filaId, comando, cancellationToken);
                return ARespuesta(resultado);
            })
            .WithName("EditarFilaParcial")
            .WithSummary("Reemplaza los campos de una fila de la cuadrícula");

        group.MapDelete("/filas/{filaId:int}", async (int filaId, IPlanillaMutaciones mutaciones, CancellationToken cancellationToken) =>
            {
                var resultado = await mutaciones.EliminarFilaAsync(filaId, cancellationToken);
                return ARespuesta(resultado);
            })
            .WithName("EliminarFilaParcial")
            .WithSummary("Elimina una fila de la cuadrícula");

        group.MapPost("/confirmar", async (ComandoConfirmar comando, IPlanillaMutaciones mutaciones, CancellationToken cancellationToken) =>
            {
                var resultado = await mutaciones.ConfirmarAsync(comando.AlumnoId, cancellationToken);
                if (resultado.Falla is { } falla)
                {
                    return Problema(falla);
                }

                return Results.Ok(resultado.Vista);
            })
            .WithName("ConfirmarPlanilla")
            .WithSummary("Confirma la planilla (solo sesión en Lista): snapshot congelado");

        return endpoints;
    }

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

    // Las mutaciones devuelven la vista de sesión refrescada (la SPA reemplaza
    // su estado con una sola llamada) o el ProblemDetails de la falla.
    private static IResult ARespuesta(ResultadoMutacion<RespuestaSesionActual> resultado) =>
        resultado.Falla is { } falla
            ? Problema(falla)
            : Results.Ok(resultado.Vista);

    private static IResult Problema(PlanillaFalla falla) => Results.Problem(
        statusCode: falla.EstadoHttp,
        title: falla.Titulo,
        detail: falla.Detalle);
}
