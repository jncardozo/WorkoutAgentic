using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Auth;

namespace WorkoutAgentic.Api.Features.Legal;

public sealed record AcceptAgreementRequest(string Version, string Hash, bool Accepted);

internal static class LegalModule
{
    public static IEndpointRouteBuilder MapLegalAgreement(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/legal").WithTags("Acuerdo");
        group.MapGet("/agreement", (AgreementDocument document) => Results.Ok(document.Current));
        group.MapPost("/accept", Accept).RequireAuthorization();
        group.MapGet("/receipt", Receipt).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> Accept(AcceptAgreementRequest request, HttpContext http,
        UserManager<ApplicationUser> users, CapturasDbContext db, AgreementDocument document, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(http.User);
        var student = user is null ? null : await db.Alumnos.FindAsync([user.StudentId], cancellationToken);
        if (user is null || student is null || student.Estado != "ACTIVO") return Results.Unauthorized();
        if (!document.Current.Available)
            return Results.Problem(statusCode: 503, title: "El acuerdo aún no está disponible. Contactá al responsable de la prueba.");
        if (!request.Accepted)
            return Results.Problem(statusCode: 400, title: "Marcá la casilla para aceptar el acuerdo y continuar.");
        if (request.Version != document.Current.Version || request.Hash != document.Current.Hash)
            return Results.Problem(statusCode: 409, title: "El acuerdo cambió. Recargá la página y revisá la nueva versión antes de aceptar.");
        if (await document.HasAcceptedAsync(db, user.Id, cancellationToken)) return Results.NoContent();

        db.AgreementAcceptances.Add(new AgreementAcceptance
        {
            UserId = user.Id, Version = document.Current.Version, DocumentHash = document.Current.Hash,
            DocumentText = document.Current.Text, ParticipantName = student.Nombre, ParticipantEmail = user.Email!,
            AcceptedAtUtc = DateTime.UtcNow,
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            // Concurrent submissions for the same account are idempotent.
            if (!await document.HasAcceptedAsync(db, user.Id, cancellationToken)) throw;
        }
        return Results.NoContent();
    }

    private static async Task<IResult> Receipt(HttpContext http, UserManager<ApplicationUser> users,
        CapturasDbContext db, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(http.User);
        if (user is null || !await db.Alumnos.AnyAsync(a => a.Id == user.StudentId && a.Estado == "ACTIVO", cancellationToken))
            return Results.Unauthorized();
        var acceptance = await db.AgreementAcceptances.AsNoTracking().Where(a => a.UserId == user.Id)
            .OrderByDescending(a => a.Id).FirstOrDefaultAsync(cancellationToken);
        if (acceptance is null) return Results.NotFound();
        var receipt = $"CONSTANCIA DE ACEPTACIÓN\nParticipante: {acceptance.ParticipantName}\nCorreo: {acceptance.ParticipantEmail}\n"
            + $"Fecha UTC: {acceptance.AcceptedAtUtc:yyyy-MM-dd HH:mm:ss} UTC\nVersión: {acceptance.Version}\nSHA-256: {acceptance.DocumentHash}\n\n"
            + acceptance.DocumentText;
        return Results.File(Encoding.UTF8.GetBytes(receipt), "text/plain; charset=utf-8", "WorkoutAgentic-acuerdo-aceptado.txt");
    }
}
