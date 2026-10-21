namespace WorkoutAgentic.Api.Data;

// Unidades del registro (spec NLU §2): kg por defecto en ejercicios con peso,
// tiempo para plancha/dinámica, peso_corporal para "al cuerpo", lb si corresponde.
public enum Unidad
{
    Kg,
    Lb,
    PesoCorporal,
    Tiempo,
}
