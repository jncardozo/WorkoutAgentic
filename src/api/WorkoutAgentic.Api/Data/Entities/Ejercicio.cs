namespace WorkoutAgentic.Api.Data.Entities;

// Catálogo canónico (glosario): nombre normalizado + apodos coloquiales para
// el CatalogResolver (ADR S2). Es determinístico y auditable.
public class Ejercicio
{
    public int Id { get; set; }

    public string NombreCanonico { get; set; } = string.Empty;

    // Colección primitiva → texto JSON (EF Core 8+), portable a Postgres.
    public List<string> Apodos { get; set; } = [];

    // Grupo muscular (español coloquial, catálogo curado).
    public string GrupoMuscular { get; set; } = string.Empty;

    // Ejercicios sin peso (plancha, dominadas) NO disparan la aclaración de peso.
    public bool UsaPeso { get; set; }

    // Plancha u otro por tiempo: unidad = Tiempo (segundos).
    public bool UsaTiempo { get; set; }

    public List<RegistroParcial> Registros { get; set; } = [];

    public List<SesionCaptura> SesionesActivas { get; set; } = [];
}
