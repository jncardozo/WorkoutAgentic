using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.Nlu;

// Lectura del catálogo desde EF (scope por mensaje). Ordena por Id para que
// los empates del resolver sean determinísticos entre corridas.
internal sealed class EjercicioCatalogoSource(CapturasDbContext db) : IWhatsappCatalogoEjercicios
{
    public async Task<IReadOnlyList<CatalogoEjercicio>> LeerAsync(CancellationToken cancellationToken) =>
        await db.Ejercicios
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new CatalogoEjercicio(
                e.Id,
                e.NombreCanonico,
                e.Apodos,
                e.GrupoMuscular,
                e.UsaPeso,
                e.UsaTiempo))
            .ToListAsync(cancellationToken);
}
