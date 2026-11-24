using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

public sealed partial class SeriesCaptureTests
{
    [Theory]
    [InlineData("Peso muerto", "RM")]
    [InlineData("Sentadilla hack", "repetición máxima")]
    [InlineData("Remo con barra", "uno RM")]
    [InlineData("Curl con barra", "erre eme")]
    public async Task Capture_OneRepMaxProviderContract_PreservesExerciseAndTarget(string exercise, string expression)
    {
        // Recorded contract: validates schema parsing and application, not a live LLM.
        var output = $$"""
            {"intent":"CARGA_EJERCICIO","confianza":1,"ejercicio":"{{exercise}}",
             "ejercicio_normalizado":"{{exercise}}","cantidad_series":2,
             "series":[{"serie":1,"repeticiones":8,"peso":40},
               {"serie":2,"repeticiones":1,"peso":100,"es_rm":true,"comentarios":"efectiva"}]}
            """;
        using var http = new HttpClient(new NluResponseHandler(output));
        var nlu = new GeminiNlu(http, Options.Create(new GeminiOptions { ApiKey = "test" }), NullLogger<GeminiNlu>.Instance);
        var pipeline = new CapturaTextoNlu(nlu, nlu, NullLogger<CapturaTextoNlu>.Instance);
        var analysis = await pipeline.AnalizarTextoAsync($"{exercise} dos series primera ocho con 40 segunda una con 100 {expression}", default);
        await service.AplicarTextoAsync(studentId, analysis!, now, default);
        var rows = await Rows();
        Assert.False(rows[0].IsOneRepMax);
        Assert.True(rows[1].IsOneRepMax);
        Assert.Equal(exercise, rows[1].EjercicioCanonico);
        Assert.Equal("efectiva", rows[1].Comments);
    }

    [Theory]
    [InlineData(null, 100, Unidad.Kg)]
    [InlineData(2, 170, Unidad.Kg)]
    [InlineData(1, 0, Unidad.Kg)]
    [InlineData(1, null, Unidad.PesoCorporal)]
    public async Task Capture_InvalidOneRepMax_RequestsClarificationWithoutCreatingRows(int? reps, int? weight, Unidad unit)
    {
        var result = await Apply(Entities(count: 2, reps: 8, weight: 40) with
        {
            Series = [new(2, Reps: reps, Weight: weight, Unit: unit, IsOneRepMax: true)],
        });
        Assert.Contains("RM", result.MensajeAlumno);
        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task Capture_OneRepMaxInPounds_ConvertsAndPreservesUntilExplicitlyRemoved()
    {
        await Apply(Entities(count: 1, reps: 1, weight: 220) with { Unidad = Unidad.Lb, IsOneRepMax = true });
        await Apply(Entities(null) with { Series = [new(1, Observations: "Sin molestias")] });
        var row = Assert.Single(await Rows());
        Assert.True(row.IsOneRepMax);
        Assert.Equal(99.79m, row.PesoKg);
        await Apply(Entities(null) with { Series = [new(1, IsOneRepMax: false)] });
        Assert.False(Assert.Single(await Rows()).IsOneRepMax);
    }

    [Fact]
    public async Task Capture_UpdateWouldInvalidateExistingOneRepMax_RejectsAllTargets()
    {
        await Apply(Entities(count: 2, reps: 1, weight: 40) with { Series = [new(2, IsOneRepMax: true)] });
        var result = await Apply(Entities(null) with { Series = [new(1, Weight: 60), new(2, Reps: 2)] });
        Assert.Contains("RM", result.MensajeAlumno);
        var rows = await Rows();
        Assert.All(rows, r => Assert.Equal(40, r.PesoKg));
        Assert.Equal(1, rows[1].Reps);
        Assert.True(rows[1].IsOneRepMax);
    }

    [Fact]
    public async Task Edit_OneRepMaxValidationAndBulkConflict_PreventInvalidOrStaleChanges()
    {
        await Apply(Entities(count: 1, reps: 1, weight: 100));
        var row = Assert.Single(await Rows());
        var mutations = new PlanillaMutaciones(db);
        var original = new ComandoEdicionFila(null, "Press de banca", 1, 1, 100, "Kg", null);
        var marked = original with { IsOneRepMax = true };
        Assert.Null((await mutations.EditarFilaAsync(row.Id, marked, default)).Falla);
        var invalid = await mutations.EditarFilaAsync(row.Id, marked with { Reps = 2 }, default);
        Assert.Equal(400, invalid.Falla!.EstadoHttp);
        var conflict = await mutations.BulkEditAsync(studentId, new(row.SesionId, [new(row.Id, original, original with { PesoKg = 120 })]), default);
        Assert.Equal(409, conflict.Falla!.EstadoHttp);
        Assert.True(Assert.Single(await Rows()).IsOneRepMax);
    }

    [Fact]
    public async Task Confirm_HighestPriorSameExerciseAndStudent_FreezesReferenceAcrossDeletion()
    {
        var date = DateTime.UtcNow.AddDays(-21);
        var reference = await SeedMax(studentId, "Press de banca", 170, date);
        await SeedMax(studentId, "Press de banca", 150, date.AddDays(7)); // Latest is smaller.
        await SeedMax(studentId, "Press de banca con mancuernas", 300, date);
        await SeedMax(studentId, "Press de banca", 220, DateTime.UtcNow.AddDays(1)); // Future.
        var other = new Alumno { Nombre = "Otro alumno" };
        db.Alumnos.Add(other); await db.SaveChangesAsync();
        await SeedMax(other.Id, "Press de banca", 250, date);
        await Apply(Entities(" press de banca ", count: 1, reps: 6, weight: 100));
        var confirmed = await new PlanillaMutaciones(db).ConfirmarAsync(studentId, default);
        Assert.Null(confirmed.Falla);
        var progress = Assert.Single(confirmed.Vista!.OneRepMaxProgress!);
        Assert.Equal(170, progress.ReferenceKg);
        Assert.Equal(58.8m, progress.Percentage);
        Assert.Equal(reference.Id, progress.ReferencePlanillaId);

        reference.DeletedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var frozen = await db.PlanillasConfirmadas.Include(p => p.Filas).SingleAsync(p => p.Id == confirmed.Vista.Id);
        Assert.Equal(progress, Assert.Single(OneRepMaxProgress.Summarize(frozen)));

        await Apply(Entities(count: 1, reps: 1, weight: 180) with { IsOneRepMax = true });
        var next = await new PlanillaMutaciones(db).ConfirmarAsync(studentId, default);
        var nextProgress = Assert.Single(next.Vista!.OneRepMaxProgress!);
        Assert.Equal(150, nextProgress.ReferenceKg); // Deleted RM excluded from new reports.
        Assert.Equal(120, nextProgress.Percentage); // Never clamp percentages to 100.
        Assert.True(Assert.Single(next.Vista.Filas).IsOneRepMax);
    }

    [Fact]
    public async Task Confirm_FirstOneRepMax_DoesNotInventEarlierReference()
    {
        await Apply(Entities("Peso muerto", count: 1, reps: 1, weight: 140) with { IsOneRepMax = true });
        var confirmed = await new PlanillaMutaciones(db).ConfirmarAsync(studentId, default);
        Assert.True(Assert.Single(confirmed.Vista!.Filas).IsOneRepMax);
        Assert.Null(Assert.Single(confirmed.Vista.OneRepMaxProgress!).ReferenceKg);
        Assert.True((await db.PlanillasFilas.SingleAsync()).IsOneRepMax);
    }

    private async Task<PlanillaConfirmada> SeedMax(int student, string exercise, decimal weight, DateTime date)
    {
        var plan = new PlanillaConfirmada
        {
            AlumnoId = student, ConfirmadaUtc = date,
            Sesion = new SesionCaptura { AlumnoId = student, Estado = EstadoSesionCaptura.Confirmada, UltimaActividadUtc = date },
            Filas = [new() { Orden = 1, EjercicioCanonical = exercise, NroSerie = 1, Reps = 1, PesoKg = weight, Unidad = Unidad.Kg, IsOneRepMax = true }],
        };
        db.PlanillasConfirmadas.Add(plan); await db.SaveChangesAsync();
        return plan;
    }
}
