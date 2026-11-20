using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.PlanillaApi;
using WorkoutAgentic.Api.Features.Legal;

namespace WorkoutAgentic.Api.Features.Auth;

// All legacy identifiers are checked against the authenticated account. Never
// trust a student id supplied by a route, query string or request body.
internal sealed class StudentAccessFilter(CapturasDbContext db, UserManager<ApplicationUser> users, AgreementDocument agreement) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = await users.GetUserAsync(http.User);
        if (user is null || !await db.Alumnos.AnyAsync(a => a.Id == user.StudentId && a.Estado == "ACTIVO"))
            return Results.Unauthorized();

        if (!await agreement.HasAcceptedAsync(db, user.Id, http.RequestAborted))
            return AgreementDocument.AcceptanceRequired();

        var ids = new List<int>();
        if (http.Request.RouteValues.TryGetValue("alumnoId", out var routeId) && int.TryParse(routeId?.ToString(), out var routeStudent))
            ids.Add(routeStudent);
        if (http.Request.Query.TryGetValue("alumnoId", out var queryId))
        {
            if (!int.TryParse(queryId, out var queryStudent)) return Results.BadRequest();
            ids.Add(queryStudent);
        }
        foreach (var argument in context.Arguments)
        {
            switch (argument)
            {
                case ComandoNuevaFila command: ids.Add(command.AlumnoId); break;
                case ComandoConfirmar command: ids.Add(command.AlumnoId); break;
                case ComandoEnviarPlanillaCorreo command: ids.Add(command.AlumnoId); break;
            }
        }
        if (ids.Any(id => id != user.StudentId)) return Results.Forbid();

        if (http.Request.RouteValues.TryGetValue("filaId", out var rowId) && int.TryParse(rowId?.ToString(), out var row))
        {
            var owner = await db.RegistrosParciales.Where(r => r.Id == row).Select(r => (int?)r.Sesion!.AlumnoId).SingleOrDefaultAsync();
            if (owner != user.StudentId) return Results.NotFound();
        }
        if (http.Request.RouteValues.TryGetValue("planillaId", out var sheetId) && int.TryParse(sheetId?.ToString(), out var sheet))
        {
            if (!await db.PlanillasConfirmadas.AnyAsync(p => p.Id == sheet && p.AlumnoId == user.StudentId))
                return Results.NotFound();
        }
        return await next(context);
    }
}
