using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

// API de captura y cuadrícula del alumno, incluyendo confirmación de snapshot.
internal static class PlanillaApiModule
{
    public static IServiceCollection AddPlanillaApi(this IServiceCollection services)
    {
        services.AddScoped<IPlanillaConsulta, PlanillaConsulta>();
        services.AddScoped<IPlanillaMutaciones, PlanillaMutaciones>();
        services.AddOptions<CorreoSmtpOptions>().BindConfiguration(CorreoSmtpOptions.SectionName);
        services.AddScoped<PlanillaCorreoSender>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlanillaApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/planillas").WithTags("Planillas");

        group.MapGet("/sesion-actual/{alumnoId:int}", ObtenerSesionActual)
            .WithName("ObtenerSesionActual")
            .WithSummary("Sesión hablante del alumno con la cuadrícula de parciales");

        group.MapGet("/historial/{alumnoId:int}", ObtenerHistorial)
            .WithName("ObtenerHistorialPlanillas")
            .WithSummary("Historial de planillas confirmadas del alumno");

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

        group.MapDelete("/sesion-actual/{alumnoId:int}/filas", async (int alumnoId, IPlanillaMutaciones mutaciones, CancellationToken cancellationToken) =>
            {
                var resultado = await mutaciones.EliminarTodasLasFilasAsync(alumnoId, cancellationToken);
                return ARespuesta(resultado);
            })
            .WithName("EliminarTodasLasFilasParciales")
            .WithSummary("Elimina todas las filas de la sesión editable del alumno");

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

        group.MapGet("/confirmadas/{planillaId:int}/excel", DescargarExcel)
            .WithName("DescargarPlanillaExcel")
            .WithSummary("Descarga una planilla confirmada en formato Excel");

        group.MapGet("/confirmadas/{planillaId:int}/pdf", DescargarPdf)
            .WithName("DescargarPlanillaPdf")
            .WithSummary("Descarga una planilla confirmada en formato PDF");

        group.MapPost("/confirmadas/{planillaId:int}/correo", EnviarPorCorreo)
            .WithName("EnviarPlanillaPorCorreo")
            .WithSummary("Envía una planilla confirmada como Excel o PDF");

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

    private static async Task<IResult> ObtenerHistorial(
        int alumnoId,
        IPlanillaConsulta consulta,
        CancellationToken cancellationToken)
    {
        var historial = await consulta.HistorialAsync(alumnoId, cancellationToken);
        return historial is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Alumno inexistente")
            : Results.Ok(historial);
    }

    private static async Task<IResult> DescargarExcel(
        int planillaId,
        int alumnoId,
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        var planilla = await CargarPlanillaConfirmada(planillaId, alumnoId, db, cancellationToken);
        return planilla is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Planilla inexistente")
            : Archivo(PlanillaArchivoFactory.Excel(planilla));
    }

    private static async Task<IResult> DescargarPdf(
        int planillaId,
        int alumnoId,
        CapturasDbContext db,
        CancellationToken cancellationToken)
    {
        var planilla = await CargarPlanillaConfirmada(planillaId, alumnoId, db, cancellationToken);
        return planilla is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Planilla inexistente")
            : Archivo(PlanillaArchivoFactory.Pdf(planilla));
    }

    private static async Task<IResult> EnviarPorCorreo(
        int planillaId,
        ComandoEnviarPlanillaCorreo comando,
        CapturasDbContext db,
        PlanillaCorreoSender sender,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comando.Correo)
            || !string.Equals(comando.Correo, comando.Correo.Trim(), StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Correo inválido",
                detail: "Escribí una dirección de correo válida.");
        }

        try
        {
            _ = new MailAddress(comando.Correo);
        }
        catch (FormatException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Correo inválido",
                detail: "Escribí una dirección de correo válida.");
        }

        var archivo = comando.Formato.Trim().ToLowerInvariant() switch
        {
            "excel" or "xlsx" => "excel",
            "pdf" => "pdf",
            _ => null,
        };
        if (archivo is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Formato inválido",
                detail: "Elegí Excel o PDF.");
        }

        var planilla = await CargarPlanillaConfirmada(planillaId, comando.AlumnoId, db, cancellationToken);
        if (planilla is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Planilla inexistente");
        }

        var exportado = archivo == "excel"
            ? PlanillaArchivoFactory.Excel(planilla)
            : PlanillaArchivoFactory.Pdf(planilla);
        var resultado = await sender.EnviarAsync(comando.Correo, exportado, cancellationToken);
        return resultado switch
        {
            EstadoEnvioCorreo.Enviado => Results.Ok(new RespuestaEnviarPlanillaCorreo(
                $"La planilla se envió a {comando.Correo}.")),
            EstadoEnvioCorreo.ConfiguracionIncompleta => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Correo no configurado",
                detail: "Configurá el servidor SMTP de la aplicación antes de enviar correos."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "No se pudo enviar el correo",
                detail: "Revisá la configuración SMTP e intentá nuevamente."),
        };
    }

    private static Task<WorkoutAgentic.Api.Data.Entities.PlanillaConfirmada?> CargarPlanillaConfirmada(
        int planillaId,
        int alumnoId,
        CapturasDbContext db,
        CancellationToken cancellationToken) => db.PlanillasConfirmadas
        .AsNoTracking()
        .Include(p => p.Filas)
        .FirstOrDefaultAsync(p => p.Id == planillaId && p.AlumnoId == alumnoId, cancellationToken);

    private static IResult Archivo(PlanillaArchivo archivo) =>
        Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);

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
