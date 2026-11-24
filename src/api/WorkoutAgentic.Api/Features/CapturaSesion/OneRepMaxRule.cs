using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

internal static class OneRepMaxRule
{
    internal const string Clarification = "El RM corresponde al máximo para una sola repetición con peso mayor que cero. Indicá la serie, una repetición y su peso, o aclará que no fue RM; no cambié las filas.";

    internal static bool IsValid(bool isOneRepMax, int? reps, decimal? weight, Unidad? unit, int? seconds = null) =>
        !isOneRepMax || (reps == 1 && weight is > 0 && unit is Unidad.Kg or Unidad.Lb && seconds is null);
}
