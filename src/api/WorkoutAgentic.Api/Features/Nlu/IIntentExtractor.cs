namespace WorkoutAgentic.Api.Features.Nlu;

// Etapa 2 (ADR-0002 S2): el LLM clasifica el intent.
public interface IIntentClassifier
{
    Task<ClasificacionIntent> ClasificarAsync(string texto, CancellationToken cancellationToken);
}

// Etapa 3 (ADR-0002 S2): el LLM extrae entidades normalizadas (texto crudo del
// ejercicio; el CatalogResolver asigna el EjercicioId determinísticamente).
public interface IEntityExtractor
{
    Task<EntidadesExtraidas> ExtraerAsync(string texto, CancellationToken cancellationToken);
}
