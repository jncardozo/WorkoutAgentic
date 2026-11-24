using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Features.CapturaSesion;

internal sealed partial class SesionCapturaServicio
{
    private const string InvalidDetails =
        "No pude asignar los datos con seguridad. Indicá el número de cada serie y sus repeticiones, peso o notas; no cambié las filas.";

    private static SeriesDetail CommonData(EntidadesNlu entities) => new(
        entities.SerieOrdinal ?? 0, entities.Reps, entities.PesoKg, entities.Unidad,
        entities.Comments, entities.Observations, entities.IsOneRepMax);

    private static bool HasData(SeriesDetail detail) => detail.Reps is not null
        || detail.Weight is not null || detail.Unit == Unidad.PesoCorporal
        || !string.IsNullOrWhiteSpace(detail.Comments) || !string.IsNullOrWhiteSpace(detail.Observations)
        || detail.IsOneRepMax is not null;

    private static bool InvalidValues(SeriesDetail detail) => detail.Reps is < 1
        || detail.Weight is < 0
        || (detail.Unit == Unidad.PesoCorporal && detail.Weight is not null)
        || (detail.Unit == Unidad.Tiempo && detail.Weight is not null);

    private static string? ValidateCapture(AnalisisCaptura analysis)
    {
        if (!analysis.Clasificacion.Exito) return TextosBot.FalloLectura;
        if (!EsIntentoDeCarga(analysis)) return null;
        if (analysis.Entidades is not { } entities) return TextosBot.FalloLectura;
        if (entities.CampoAclarar is { Length: > 0 } field && field != "ejercicio")
            return entities.PreguntaAclarar ?? InvalidDetails;
        if (entities.CantidadSeries is < 1 or > MaxSeriesLote || entities.SerieOrdinal is < 1)
            return InvalidDetails;
        if (InvalidValues(CommonData(entities))) return InvalidDetails;
        if (entities.CantidadSeries is > 1 && entities.IsOneRepMax is not null)
            return "Indicá en qué serie registraste el RM; no cambié las filas.";
        if (entities.CantidadSeries is { } batchCount)
        {
            for (var index = 1; index <= batchCount; index++)
            {
                var candidate = new RegistroParcial();
                ApplyValues(candidate, CommonData(entities));
                if (entities.Series?.FirstOrDefault(d => d.Ordinal == index) is { } detail)
                    ApplyValues(candidate, detail);
                if (!OneRepMaxRule.IsValid(candidate.IsOneRepMax, candidate.Reps, candidate.PesoKg, candidate.Unidad))
                    return OneRepMaxRule.Clarification;
            }
        }
        if (entities.Series is not { Count: > 0 } details) return null;
        if (details.Count > MaxSeriesLote || details.Select(d => d.Ordinal).Distinct().Count() != details.Count
            || details.Any(d => d.Ordinal < 1 || InvalidValues(d)
                || (entities.CantidadSeries is null && !HasData(d))))
            return InvalidDetails;
        if (entities.CantidadSeries is { } count && details.Any(d => d.Ordinal > count))
            return InvalidDetails;
        // Updates carry values in the details, or in the legacy single-series fields.
        // Mixing both representations without an explicit batch is ambiguous.
        if (entities.CantidadSeries is null && HasData(CommonData(entities))) return InvalidDetails;
        return null;
    }

    private static void ApplyValues(RegistroParcial row, SeriesDetail detail)
    {
        if (detail.Reps is { } reps) row.Reps = reps;
        if (detail.Weight is not null || detail.Unit == Unidad.PesoCorporal)
        {
            (row.PesoKg, row.Unidad) = NormalizarValores(detail.Weight, detail.Unit);
        }
        if (!string.IsNullOrWhiteSpace(detail.Comments)) row.Comments = detail.Comments.Trim();
        if (!string.IsNullOrWhiteSpace(detail.Observations)) row.Observations = detail.Observations.Trim();
        if (detail.IsOneRepMax is { } isOneRepMax) row.IsOneRepMax = isOneRepMax;
    }

    private static string BatchMessage(string name, IReadOnlyList<RegistroParcial> rows)
    {
        var message = $"Anotadas {rows.Count} series de {name} ({rows.Min(r => r.NroSerie)} a {rows.Max(r => r.NroSerie)}). Revisá los datos en la cuadrícula.";
        var incomplete = rows.Where(r => !ReglaCompletitudSeries.EstaCompleta(r)).ToList();
        return incomplete.Count == 0 ? message : message + " " + ReglaCompletitudSeries.MensajeParaCompletar(incomplete);
    }

    private async Task<CapturaRespuesta> AplicarDatosSerieAsync(
        SesionCaptura session, AnalisisCaptura analysis, CancellationToken cancellationToken)
    {
        var entities = analysis.Entidades!;
        IReadOnlyList<SeriesDetail> details = entities.Series is { Count: > 0 } multiple
            ? multiple : [CommonData(entities)];
        var all = await db.RegistrosParciales.Include(r => r.Ejercicio)
            .Where(r => r.SesionId == session.Id).ToListAsync(cancellationToken);
        if (all.Count == 0) return new(TextosBot.FueraDeRegla, session.Id, false);

        var name = entities.EjercicioNormalizado ?? entities.EjercicioTexto;
        var candidates = string.IsNullOrWhiteSpace(name)
            ? all.Where(r => r.CreadoUtc == all.Max(f => f.CreadoUtc)).ToList()
            : all.Where(r => MatchesExercise(r, name, entities.EjercicioTexto)).ToList();
        if (entities.CampoAclarar == "ejercicio" && name is not null)
            return new(TextosBot.PreguntaEjercicioAbierta(entities.PreguntaAclarar), session.Id, false);

        name ??= candidates.FirstOrDefault()?.EjercicioCanonico
            ?? candidates.FirstOrDefault()?.EjercicioCrudo ?? "el ejercicio";
        var targets = new List<(RegistroParcial Row, SeriesDetail Detail)>();
        foreach (var detail in details)
        {
            var matches = candidates.Where(r => r.NroSerie == detail.Ordinal).ToList();
            if (matches.Count == 0)
                return new(TextosBot.SerieSinFila(name, detail.Ordinal), session.Id, false);
            if (matches.Count != 1)
                return new($"Hay más de una serie {detail.Ordinal} de {name}. Editá la fila correspondiente en la cuadrícula; no cambié los datos.", session.Id, false);
            var candidate = new RegistroParcial
            {
                Reps = matches[0].Reps, PesoKg = matches[0].PesoKg, Unidad = matches[0].Unidad,
                TiempoSeg = matches[0].TiempoSeg, IsOneRepMax = matches[0].IsOneRepMax,
            };
            ApplyValues(candidate, detail);
            if (!OneRepMaxRule.IsValid(candidate.IsOneRepMax, candidate.Reps, candidate.PesoKg, candidate.Unidad, candidate.TiempoSeg))
                return new(OneRepMaxRule.Clarification, session.Id, false);
            targets.Add((matches[0], detail));
        }

        // Validate every destination before changing any row: no partial updates.
        foreach (var (row, detail) in targets)
        {
            ApplyValues(row, detail);
            row.PendienteAclaracion = false;
        }
        var message = targets.Count == 1
            ? TextosBot.SerieConDatos(name, targets[0].Row.NroSerie,
                TextosBot.DetalleSerie(targets[0].Row.Reps, targets[0].Row.PesoKg, targets[0].Row.Unidad))
            : $"Actualizadas {targets.Count} series de {name}.";
        if (targets.Any(t => t.Detail.Comments is not null || t.Detail.Observations is not null))
            message += " Notas guardadas en la cuadrícula.";
        var incomplete = targets.Select(t => t.Row).Where(r => !ReglaCompletitudSeries.EstaCompleta(r)).ToList();
        if (incomplete.Count > 0) message += " " + ReglaCompletitudSeries.MensajeParaCompletar(incomplete);
        return new(message, session.Id, false);
    }

    private static bool MatchesExercise(RegistroParcial row, string normalized, string? raw) =>
        string.Equals(row.EjercicioCanonico ?? row.Ejercicio?.NombreCanonico, normalized, StringComparison.OrdinalIgnoreCase)
        || (raw is not null && string.Equals(row.EjercicioCrudo, raw, StringComparison.OrdinalIgnoreCase));
}
