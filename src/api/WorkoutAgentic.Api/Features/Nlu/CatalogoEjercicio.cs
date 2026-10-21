namespace WorkoutAgentic.Api.Features.Nlu;

// Proyección de lectura del catálogo (Data.Entities.Ejercicio) para el
// CatalogResolver. Sin EF aquí: unit-tested puro.
public sealed record CatalogoEjercicio(
    int Id,
    string NombreCanonico,
    IReadOnlyList<string> Apodos,
    string GrupoMuscular,
    bool UsaPeso,
    bool UsaTiempo);
