using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Features.CapturaTexto;

// Comando del borde (identificadores en inglés, UI en español).
public sealed record ComandoTextoCaptura(string Texto);

// La captura por texto devuelve el MISMO contrato que la SPA ya consume
// (sesión refrescada) + el mensaje del bot para el banner de la PWA.
public sealed record RespuestaCapturaTextoVista(RespuestaSesionActual Respuesta, string MensajeAlumno);

// Slice captura por texto (ADR-0009): el alumno dicta (teclado del celular) o
// escribe; la PWA envía el texto crudo y reutiliza el pipeline NLU → sesión →
// respuesta del bot SIN transporte de WhatsApp.
internal static class CapturaTextoModule
{
    public static IEndpointRouteBuilder MapCapturaTexto(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/capturas").WithTags("Captura");

        group.MapPost("/{alumnoId:int}/texto", Capturar)
            .WithName("CapturarTexto")
            .WithSummary("Aplica un mensaje dictado/escrito por el alumno y devuelve la sesión refrescada");

        return endpoints;
    }

    private static async Task<IResult> Capturar(
        int alumnoId,
        ComandoTextoCaptura comando,
        CapturasDbContext db,
        IMensajesNlu mensajesNlu,
        ISesionCapturaServicio servicio,
        IPlanillaConsulta consulta,
        CancellationToken cancellationToken)
    {
        var texto = comando.Texto?.Trim() ?? string.Empty;
        if (texto.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Texto requerido",
                detail: "Escribí o dictá el ejercicio y volvé a enviar.");
        }

        var alumno = await db.Alumnos.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == alumnoId, cancellationToken);
        if (alumno is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Alumno inexistente");
        }

        var analisis = await mensajesNlu.AnalizarTextoAsync(texto, $"spa-{alumnoId}", cancellationToken);
        if (analisis is null)
        {
            // Una etapa del análisis falló (sin dato inventado): estado 503,
            // reintentable — nada se aplicó a la sesión.
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "No pudimos interpretar la captura",
                detail: "La etapa de análisis no devolvió resultado. Probá otra vez en unos segundos.");
        }

        var respuesta = await servicio.AplicarTextoAsync(alumnoId, analisis, DateTime.UtcNow, cancellationToken);

        var sesionActual = await consulta.SesionActualAsync(alumnoId, cancellationToken);
        if (sesionActual is null)
        {
            // Paranoya conservadora: el alumno existía al validar el borde.
            throw new InvalidOperationException(
                $"Alumno {alumnoId} desapareció entre aplicar el mensaje y leer la sesión.");
        }

        return Results.Ok(new RespuestaCapturaTextoVista(sesionActual, respuesta.MensajeAlumno));
    }
}
