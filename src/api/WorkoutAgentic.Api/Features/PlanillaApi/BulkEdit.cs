using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

public sealed record BulkRowEdit(int Id, ComandoEdicionFila? Original, ComandoEdicionFila? Values);
public sealed record BulkEditRequest(int SessionId, IReadOnlyList<BulkRowEdit>? Rows);

internal sealed partial class PlanillaMutaciones
{
    private static PlanillaFalla EditConflict() => new(409, "La planilla cambió",
        "Otra carga modificó estas series. Conservamos tu borrador: revisá la versión actual antes de volver a guardar.");

    public async Task<ResultadoMutacion<RespuestaSesionActual>> BulkEditAsync(
        int studentId, BulkEditRequest request, CancellationToken cancellationToken)
    {
        if (request.Rows is not { Count: > 0 } changes || changes.Any(r => r is null || r.Original is null || r.Values is null)
            || changes.Select(r => r.Id).Distinct().Count() != changes.Count)
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(PlanillaFalla.DatosInvalidos("Seleccioná filas distintas con sus valores originales y nuevos."));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (session, student, failure) = await SesionEditableAsync(studentId, cancellationToken);
        if (failure is not null || session is null)
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(failure ?? PlanillaFalla.SesionInexistente());
        if (session.Id != request.SessionId)
            return ResultadoMutacion<RespuestaSesionActual>.ConFalla(EditConflict());

        var staged = new List<(RegistroParcial Row, PreparedEdit Edit)>();
        foreach (var change in changes)
        {
            var row = session.Registros.SingleOrDefault(r => r.Id == change.Id);
            if (row is null || CurrentValues(row) != change.Original)
                return ResultadoMutacion<RespuestaSesionActual>.ConFalla(EditConflict());
            var (edit, error) = await PrepareEditAsync(row, change.Values!, cancellationToken);
            if (error is not null)
                return ResultadoMutacion<RespuestaSesionActual>.ConFalla(error with
                {
                    Detalle = $"{row.EjercicioCanonico ?? row.Ejercicio?.NombreCanonico ?? row.EjercicioCrudo}, serie {row.NroSerie}: {error.Detalle}",
                });
            staged.Add((row, edit!));
        }

        // All validation and conflict checks finish before any tracked row is changed.
        foreach (var (row, edit) in staged) ApplyEdit(row, edit);
        session.UltimaActividadUtc = DateTime.UtcNow;
        session.RowVersion++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ResultadoMutacion<RespuestaSesionActual>.ConVista(VistaPlanilla.Armar(studentId, student.Nombre, session));
    }

    private static ComandoEdicionFila CurrentValues(RegistroParcial row) => new(
        row.EjercicioId, row.EjercicioCrudo, row.NroSerie, row.Reps, row.PesoKg,
        row.Unidad?.ToString(), row.TiempoSeg, row.Comments, row.Observations, row.IsOneRepMax);

    private sealed record PreparedEdit(Ejercicio? Exercise, string? Raw, string? Name,
        int Number, int? Reps, decimal? Weight, Data.Unidad? Unit, int? Time, string? Comments, string? Observations, bool IsOneRepMax);

    private async Task<(PreparedEdit? Edit, PlanillaFalla? Failure)> PrepareEditAsync(
        RegistroParcial row, ComandoEdicionFila command, CancellationToken cancellationToken)
    {
        var exercise = row.Ejercicio;
        var raw = row.EjercicioCrudo;
        var name = row.EjercicioCanonico;
        if (command.EjercicioId is { } id)
        {
            exercise = await db.Ejercicios.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (exercise is null) return (null, PlanillaFalla.EjercicioInexistente(id));
            name = exercise.NombreCanonico;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.EjercicioCrudo))
                return (null, PlanillaFalla.DatosInvalidos("Escribí el nombre del ejercicio."));
            exercise = null;
            raw = command.EjercicioCrudo.Trim();
            name = raw == row.EjercicioCrudo ? row.EjercicioCanonico ?? raw : raw;
        }
        var values = ValidarCuerpoFila(exercise, command.NroSerie, command.Reps, command.PesoKg, command.Unidad, command.TiempoSeg);
        if (values.Falla is not null) return (null, values.Falla);
        if (!CapturaSesion.OneRepMaxRule.IsValid(command.IsOneRepMax, values.Reps, values.PesoKg, values.Unidad, values.TiempoSeg))
            return (null, PlanillaFalla.DatosInvalidos(CapturaSesion.OneRepMaxRule.Clarification));
        return (new(exercise, raw, name, command.NroSerie, values.Reps, values.PesoKg,
            values.Unidad, values.TiempoSeg, CleanNote(command.Comments), CleanNote(command.Observations), command.IsOneRepMax), null);
    }

    private static void ApplyEdit(RegistroParcial row, PreparedEdit edit)
    {
        row.Ejercicio = edit.Exercise;
        row.EjercicioCrudo = edit.Raw;
        row.EjercicioCanonico = edit.Name;
        row.NroSerie = edit.Number;
        row.Reps = edit.Reps;
        row.PesoKg = edit.Weight;
        row.Unidad = edit.Unit;
        row.TiempoSeg = edit.Time;
        row.Comments = edit.Comments;
        row.Observations = edit.Observations;
        row.IsOneRepMax = edit.IsOneRepMax;
        row.DescripcionPendiente = row.PendienteAclaracion = false;
    }
}
