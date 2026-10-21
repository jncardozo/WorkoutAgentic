namespace WorkoutAgentic.Api.Features.Nlu;

// Estado de la resolución determinística del ejercicio (spec NLU §2/§4):
// Matcheado → hay EjercicioId; Ambiguo y SinMatch → el flujo pide aclaración
// (nada de adivinar).
public enum EstadoResolucion
{
    Matcheado,
    Ambiguo,
    SinMatch,
}

// Candidatura de sugerencia para la pregunta "¿te referís a …?".
public sealed record SugerenciaEjercicio(
    int Id,
    string NombreCanonico,
    double Score);

public sealed record ResultadoResolucionEjercicio(
    EstadoResolucion Estado,
    int? EjercicioId,
    string? EjercicioCanonico,
    IReadOnlyList<SugerenciaEjercicio> Sugerencias)
{
    public static ResultadoResolucionEjercicio Matcheado(int id, string canonico, double score) =>
        new(EstadoResolucion.Matcheado, id, canonico, [new SugerenciaEjercicio(id, canonico, score)]);

    public static ResultadoResolucionEjercicio Ambiguo(IReadOnlyList<SugerenciaEjercicio> sugerencias) =>
        new(EstadoResolucion.Ambiguo, EjercicioId: null, EjercicioCanonico: null, sugerencias);

    public static ResultadoResolucionEjercicio SinMatch(IReadOnlyList<SugerenciaEjercicio> sugerencias) =>
        new(EstadoResolucion.SinMatch, EjercicioId: null, EjercicioCanonico: null, sugerencias);
}
