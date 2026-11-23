using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

public sealed record RenameTrainingRequest(string? Name);

// El grupo autenticado verifica propietario y acuerdo mediante StudentAccessFilter.
// Updates condicionales de metadatos: no sobrescribir una eliminación concurrente.
internal sealed class TrainingHistoryChanges(CapturasDbContext db)
{
    public async Task<IResult> RenameAsync(int planillaId, RenameTrainingRequest request, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        if (name is { Length: > 120 }
            || name?.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029') == true)
            return Results.Problem(statusCode: 400, title: "Usá un nombre de hasta 120 caracteres y una sola línea.");

        var updated = await db.PlanillasConfirmadas.Where(p => p.Id == planillaId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Name, name), cancellationToken);
        if (updated == 0) return Missing();
        var sheet = await db.PlanillasConfirmadas.AsNoTracking().Include(p => p.Filas)
            .SingleOrDefaultAsync(p => p.Id == planillaId, cancellationToken);
        return sheet is null ? Missing() : Results.Ok(VistaPlanilla.MapearPlanillaConfirmada(sheet));
    }

    public async Task<IResult> DeleteAsync(int planillaId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var updated = await db.PlanillasConfirmadas.Where(p => p.Id == planillaId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.DeletedUtc, now), cancellationToken);
        return updated == 0 ? Missing() : Results.NoContent();
    }

    private static IResult Missing() => Results.Problem(statusCode: 404, title: "Planilla inexistente");
}
