namespace WorkoutAgentic.Api.Features.Nlu;

// Fuente del catálogo canónico para el resolver (lectura AsNoTracking por
// mensaje). Separa la lógica del resolver del acceso a la BD.
internal interface IWhatsappCatalogoEjercicios
{
    Task<IReadOnlyList<CatalogoEjercicio>> LeerAsync(CancellationToken cancellationToken);
}
