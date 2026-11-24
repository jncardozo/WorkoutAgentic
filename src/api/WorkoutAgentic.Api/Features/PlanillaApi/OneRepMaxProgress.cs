using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

internal static class OneRepMaxProgress
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-AR");
    private static readonly TimeZoneInfo ReportTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    // Exact normalized names only: never conflate variants, accents or equipment.
    private static string ExerciseKey(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    internal static async Task FreezeReferencesAsync(CapturasDbContext db, PlanillaConfirmada plan, CancellationToken cancellationToken)
    {
        var history = await db.PlanillasFilas.AsNoTracking()
            .Where(f => f.Planilla!.AlumnoId == plan.AlumnoId
                && f.Planilla.ConfirmadaUtc <= plan.ConfirmadaUtc
                && f.IsOneRepMax && !f.Incompleta && f.Reps == 1 && f.PesoKg > 0 && f.Unidad == Unidad.Kg)
            .Select(f => new { f.EjercicioCanonical, f.PesoKg, f.PlanillaId, f.Orden, f.Planilla!.ConfirmadaUtc })
            .ToListAsync(cancellationToken);
        var references = history.GroupBy(f => ExerciseKey(f.EjercicioCanonical))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.PesoKg)
                .ThenByDescending(f => f.ConfirmadaUtc).ThenByDescending(f => f.PlanillaId).ThenBy(f => f.Orden).First());
        foreach (var row in plan.Filas)
        {
            if (!references.TryGetValue(ExerciseKey(row.EjercicioCanonical), out var reference)) continue;
            row.ReferenceOneRepMaxKg = reference.PesoKg;
            row.ReferenceConfirmedUtc = reference.ConfirmadaUtc;
            row.ReferencePlanillaId = reference.PlanillaId;
            row.ReferenceRowOrder = reference.Orden;
        }
    }

    internal static List<OneRepMaxProgressView> Summarize(PlanillaConfirmada plan) => plan.Filas
        .OrderBy(f => f.Orden).GroupBy(f => ExerciseKey(f.EjercicioCanonical))
        .Select(group =>
        {
            var row = group.First();
            var max = group.Where(f => f.Unidad == Unidad.Kg && f.PesoKg.HasValue)
                .Select(f => f.PesoKg).DefaultIfEmpty(null).Max();
            var reference = row.ReferenceOneRepMaxKg;
            var date = row.ReferenceConfirmedUtc is { } utc ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : (DateTime?)null;
            var percentage = max.HasValue && reference is > 0
                ? Math.Round(max.Value / reference.Value * 100m, 1, MidpointRounding.AwayFromZero) : (decimal?)null;
            var referenceText = date is { } recorded
                ? $"RM de {Number(reference)} kg (1 repetición), registrado el {TimeZoneInfo.ConvertTimeFromUtc(recorded, ReportTimeZone):dd/MM/yyyy}, planilla #{row.ReferencePlanillaId}, fila {row.ReferenceRowOrder}"
                : $"RM de {Number(reference)} kg";
            var description = percentage is { } value
                ? $"Carga máxima de la sesión: {Number(max)} kg; {value.ToString("0.#", Culture)} % del {referenceText}."
                : reference is > 0
                    ? $"{referenceText}. Sin carga numérica en esta sesión para comparar."
                    : "Sin RM previo registrado para este ejercicio.";
            return new OneRepMaxProgressView(row.EjercicioCanonical, max, reference, date,
                row.ReferencePlanillaId, row.ReferenceRowOrder, percentage, description);
        }).ToList();

    internal const string Interpretation = "El porcentaje expresa una relación de cargas; por sí solo no indica mejora ni retroceso de fuerza.";
    private static string Number(decimal? value) => value?.ToString("0.##", Culture) ?? "Sin datos";
}
