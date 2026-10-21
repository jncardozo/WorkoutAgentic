using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data;

// Catálogo MÍNIMO del MVP: cubre los fixtures del spec NLU (docs/domain/
// nlu-captura.md §6) y da base al CatalogResolver. Es seed, no dogma: el
// profesor edita/expande el catálogo en fases siguientes; este archivo no
// re-siembra sobre una tabla ya poblada (DbInitializer verifica).
public static class CatalogoEjercicioSeed
{
    public static List<Ejercicio> Crear() =>
    [
        new()
        {
            NombreCanonico = "Sillón de cuadríceps",
            Apodos = ["sillón", "sillón de cuads", "cuadríceps en máquina"],
            GrupoMuscular = "pierna",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Prensa de pierna",
            Apodos = ["prensa"],
            GrupoMuscular = "pierna",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Press de pecho en máquina",
            Apodos = ["press de pecho", "press en máquina"],
            GrupoMuscular = "pecho",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Cruces en polea",
            Apodos = ["cruces", "crucen polea"],
            GrupoMuscular = "pecho",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Remo con mancuernas",
            Apodos = ["remo", "remos", "remo con pesas"],
            GrupoMuscular = "espalda",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Curlo con mancuernas",
            Apodos = ["curl", "curlo", "biceps con mancuernas"],
            GrupoMuscular = "brazo",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Press militar con mancuernas",
            Apodos = ["militar", "press militar"],
            GrupoMuscular = "hombro",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Extensiones de tríceps en polea",
            Apodos = ["tríceps en polea", "tríceps"],
            GrupoMuscular = "brazo",
            UsaPeso = true,
        },
        new()
        {
            NombreCanonico = "Dominadas",
            Apodos = ["dominada", "barra"],
            GrupoMuscular = "espalda",
            UsaPeso = false,
        },
        new()
        {
            NombreCanonico = "Plancha",
            Apodos = ["plancha abdominal"],
            GrupoMuscular = "abdomen",
            UsaPeso = false,
            UsaTiempo = true,
        },
    ];
}
