using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

public sealed partial class SeriesCaptureTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CapturasDbContext db;
    private readonly SesionCapturaServicio service;
    private readonly int studentId;
    private DateTime now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    public SeriesCaptureTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<CapturasDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();
        var student = new Alumno { Nombre = "Alumno de prueba", Estado = "ACTIVO" };
        db.Alumnos.Add(student);
        db.SaveChanges();
        studentId = student.Id;
        service = new(db, NullLogger<SesionCapturaServicio>.Instance);
    }

    [Theory]
    [InlineData("aproximación")]
    [InlineData("entrada en calor")]
    [InlineData("calentamiento")]
    public async Task Capture_CompleteSpokenBatch_MapsAllFourRows(string comment)
    {
        // Exercise the real JSON parser and pipeline against a recorded provider contract.
        var output = JsonSerializer.Serialize(new
        {
            intent = "CARGA_EJERCICIO", confianza = 0.99,
            ejercicio = "Press de banca", ejercicio_normalizado = "Press de banca",
            cantidad_series = 4, repeticiones = 10,
            series = new[]
            {
                new { serie = 1, peso = 40, comentarios = (string?)comment },
                new { serie = 2, peso = 40, comentarios = (string?)comment },
                new { serie = 3, peso = 80, comentarios = (string?)"efectiva" },
                new { serie = 4, peso = 120, comentarios = (string?)null },
            },
        });
        using var http = new HttpClient(new NluResponseHandler(output));
        var nlu = new GeminiNlu(http, Options.Create(new GeminiOptions { ApiKey = "test" }), NullLogger<GeminiNlu>.Instance);
        var pipeline = new CapturaTextoNlu(nlu, nlu, NullLogger<CapturaTextoNlu>.Instance);
        var analysis = await pipeline.AnalizarTextoAsync(
            $"Press de banca 4 series con 10 repeticiones, las 2 primeras series de {comment} con 40 kg, la tercera serie efectiva con 80 kg y la cuarta y última serie con 120 kg", default);
        await service.AplicarTextoAsync(studentId, analysis!, now, default);
        db.ChangeTracker.Clear();
        var rows = await db.RegistrosParciales.OrderBy(r => r.NroSerie).ToListAsync();

        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.NroSerie));
        Assert.All(rows, r => { Assert.Equal(10, r.Reps); Assert.Null(r.Observations); });
        Assert.Equal(new decimal?[] { 40, 40, 80, 120 }, rows.Select(r => r.PesoKg));
        Assert.Equal(new[] { comment, comment, "efectiva", null }, rows.Select(r => r.Comments));
    }

    [Fact]
    public async Task Capture_HackSquatWithDifferentReps_PreservesEachSeriesValues()
    {
        const string text = "sentadilla hack 4 series primera  y segunda serie aproximaciones con 40kg y 12 repeticiones tercera serie efectiva con 80kg y 10 repeticiones y cuarta y ultima serie con 100kg y 8 repeticiones";
        const string output = """
            {"intent":"CARGA_EJERCICIO","confianza":1,"ejercicio":"sentadilla hack",
             "ejercicio_normalizado":"Sentadilla hack","cantidad_series":4,
             "series":[
               {"serie":1,"peso":40,"repeticiones":12,"comentarios":"aproximaciones"},
               {"serie":2,"peso":40,"repeticiones":12,"comentarios":"aproximaciones"},
               {"serie":3,"peso":80,"repeticiones":10,"comentarios":"efectiva"},
               {"serie":4,"peso":100,"repeticiones":8}]}
            """;
        // Provider contract simulated: this verifies parsing/persistence, not live LLM quality.
        using var http = new HttpClient(new NluResponseHandler(output));
        var nlu = new GeminiNlu(http, Options.Create(new GeminiOptions { ApiKey = "test" }), NullLogger<GeminiNlu>.Instance);
        var pipeline = new CapturaTextoNlu(nlu, nlu, NullLogger<CapturaTextoNlu>.Instance);
        var analysis = await pipeline.AnalizarTextoAsync(text, default);
        await service.AplicarTextoAsync(studentId, analysis!, now, default);
        var rows = await Rows();

        Assert.Equal(new int?[] { 12, 12, 10, 8 }, rows.Select(r => r.Reps));
        Assert.Equal(new decimal?[] { 40, 40, 80, 100 }, rows.Select(r => r.PesoKg));
        Assert.Equal(new[] { "aproximaciones", "aproximaciones", "efectiva", null }, rows.Select(r => r.Comments));
    }

    [Fact]
    public async Task Capture_ParticularWeight_DoesNotFillOtherRows()
    {
        await Apply(Entities(count: 4, reps: 12) with { Series = [new(1, Weight: 40)] });
        var rows = await Rows();
        Assert.Equal(new decimal?[] { 40, null, null, null }, rows.Select(r => r.PesoKg));
        Assert.All(rows, r => Assert.Equal(12, r.Reps));
    }

    [Fact]
    public async Task Capture_SpecificLoadOverridesCommonLoad_ConvertsUnits()
    {
        await Apply(Entities(count: 3, reps: 10, weight: 40) with
        {
            Series = [new(2, Weight: 40, Unit: Unidad.Lb), new(3, Unit: Unidad.PesoCorporal)],
        });
        var rows = await Rows();
        Assert.Equal(new decimal?[] { 40, 18.14m, null }, rows.Select(r => r.PesoKg));
        Assert.Equal(Unidad.PesoCorporal, rows[2].Unidad);
    }

    [Fact]
    public async Task Capture_ObservationForEarlierExercise_OnlyChangesTargetNote()
    {
        await Apply(Entities(count: 4, reps: 10, weight: 40) with { Comments = "aproximación" });
        await Apply(Entities("Remo", count: 2, reps: 12, weight: 30));
        await Apply(Entities() with
        {
            Series = [new(2, Observations: "Sentí un tirón en el pectoral izquierdo")],
        });
        var rows = await Rows();
        Assert.Equal(6, rows.Count);
        var target = Assert.Single(rows, r => r.Observations is not null);
        Assert.Equal(2, target.NroSerie);
        Assert.Equal("Press de banca", target.EjercicioCanonico);
        Assert.Equal("Sentí un tirón en el pectoral izquierdo", target.Observations);
        Assert.Equal("aproximación", target.Comments);
        Assert.Equal(10, target.Reps);
        Assert.Equal(40, target.PesoKg);
    }

    [Fact]
    public async Task Capture_MultipleUpdatesWithoutExercise_UsesLiveBatch()
    {
        await Apply(Entities(count: 2, reps: 10, weight: 40));
        await Apply(Entities("Remo", count: 2));
        await Apply(Entities(null) with { Series = [new(1, 8, 30), new(2, 6, 35, Comments: "efectiva")] });
        var rows = await Rows();
        Assert.Equal(new int?[] { 10, 10, 8, 6 }, rows.Select(r => r.Reps));
        Assert.Equal(new decimal?[] { 40, 40, 30, 35 }, rows.Select(r => r.PesoKg));
    }

    [Fact]
    public async Task Capture_MissingSecondTarget_DoesNotApplyFirstUpdate()
    {
        await Apply(Entities(count: 2, reps: 10, weight: 40));
        var result = await Apply(Entities(null) with { Series = [new(1, Weight: 80), new(3, Weight: 120)] });
        Assert.Contains("No veo la serie 3", result.MensajeAlumno);
        Assert.All(await Rows(), r => Assert.Equal(40, r.PesoKg));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("outside")]
    [InlineData("negative")]
    [InlineData("zeroReps")]
    public async Task Capture_InvalidBatch_DoesNotCreateRows(string invalid)
    {
        SeriesDetail[] details = invalid switch
        {
            "duplicate" => [new(1, Weight: 40), new(1, Weight: 80)],
            "outside" => [new(3, Weight: 40)],
            "negative" => [new(1, Weight: -1)],
            _ => [new(1, Reps: 0)],
        };
        await Apply(Entities(count: 2) with { Series = details });
        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task Capture_AmbiguousValue_LeavesSessionAndRowsUntouched()
    {
        await Apply(Entities(count: 2));
        var activity = (await db.SesionesCaptura.SingleAsync()).UltimaActividadUtc;
        var result = await Apply(Entities(null) with
        {
            CampoAclarar = "peso", PreguntaAclarar = "¿La segunda fue con 40 o con 60 kg?",
            Series = [new(2, Weight: 40)],
        });
        Assert.Contains("40 o con 60", result.MensajeAlumno);
        Assert.All(await Rows(), r => Assert.Null(r.PesoKg));
        Assert.Equal(activity, (await db.SesionesCaptura.SingleAsync()).UltimaActividadUtc);
    }

    [Fact]
    public async Task Capture_ContinuedBatch_UsesRelativeDetails()
    {
        await Apply(Entities(count: 2, reps: 10, weight: 40));
        await Apply(Entities(count: 2, reps: 8) with { Series = [new(1, Weight: 60), new(2, Weight: 80)] });
        var rows = await Rows();
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.NroSerie));
        Assert.Equal(new decimal?[] { 40, 40, 60, 80 }, rows.Select(r => r.PesoKg));
    }

    [Fact]
    public async Task Capture_PendingExercise_PreservesNotesAndQuestion()
    {
        await Apply(Entities("Press", count: 2, reps: 10, weight: 40) with { CampoAclarar = "ejercicio", Comments = "calentamiento" });
        await Apply(Entities(null) with { Series = [new(2, Observations: "Molestia a la izquierda")] });
        Assert.Equal(0, (await db.AclaracionesPendientes.SingleAsync()).Reintento);
        await Apply(Entities()); // Answer with an unambiguous exercise name.
        Assert.Empty(await db.AclaracionesPendientes.ToListAsync());
        var rows = await Rows();
        Assert.All(rows, r => { Assert.Equal("calentamiento", r.Comments); Assert.Equal(40, r.PesoKg); });
        Assert.Equal("Molestia a la izquierda", rows[1].Observations);
    }

    [Fact]
    public async Task Confirm_EditAndSnapshot_PersistsBothNotesIndependently()
    {
        await Apply(Entities(count: 1, reps: 10, weight: 40));
        var row = Assert.Single(await Rows());
        var mutations = new PlanillaMutaciones(db);
        await mutations.EditarFilaAsync(row.Id, new(null, "Press de banca", 1, 10, 40, "Kg", null,
            "efectiva", "Sentí un tirón en el pectoral izquierdo"), default);
        await mutations.ConfirmarAsync(studentId, default);
        db.ChangeTracker.Clear();
        var history = await new PlanillaConsulta(db).HistorialAsync(studentId, default);
        var frozen = Assert.Single(Assert.Single(history!).Filas);
        Assert.Equal("efectiva", frozen.Comments);
        Assert.Equal("Sentí un tirón en el pectoral izquierdo", frozen.Observations);
        await Apply(Entities("Remo", count: 1, reps: 12, weight: 20));
        Assert.Equal(frozen.Observations, (await db.PlanillasFilas.SingleAsync()).Observations);
    }

    private Task<CapturaRespuesta> Apply(EntidadesNlu entities)
    {
        now = now.AddSeconds(1);
        return service.AplicarTextoAsync(studentId, new("dictado de prueba",
            ClasificacionIntent.Ok(IntentoCaptura.CargaEjercicio, 1), entities, null), now, default);
    }

    private async Task<List<RegistroParcial>> Rows()
    {
        db.ChangeTracker.Clear();
        return await db.RegistrosParciales.OrderBy(r => r.Id).ToListAsync();
    }

    private static EntidadesNlu Entities(string? exercise = "Press de banca", int? count = null, int? reps = null, decimal? weight = null) =>
        new(exercise, null, count, reps, weight, null, null, null, null, null, exercise);

    public void Dispose() { db.Dispose(); connection.Dispose(); }

    private sealed class NluResponseHandler(string output) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[] { new { content = new { parts = new[] { new { text = output } } } } },
                }), Encoding.UTF8, "application/json"),
            });
    }
}
