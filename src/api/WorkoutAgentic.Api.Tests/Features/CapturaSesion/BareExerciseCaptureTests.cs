using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

public sealed partial class SeriesCaptureTests
{
    [Theory]
    [InlineData("press de banca inclinado con mancuernas", "Press de banca inclinado con mancuernas", false)]
    [InlineData("sillon de cuadriceps", "Sillón de cuádriceps", true)]
    public async Task Capture_ReportedBareExerciseCommand_CreatesFourEmptyRows(string exercise, string normalized, bool scaffold)
    {
        // Payload recorded from successful live Gemini calls after simplifying the schema.
        var details = scaffold ? Enumerable.Range(1, 4).Select(i => new
        {
            serie = i, repeticiones = (int?)null, peso = (decimal?)null, unidad = (string?)null,
            comentarios = (string?)null, observaciones = (string?)null, es_rm = (bool?)null,
        }).ToArray() : null;
        var output = JsonSerializer.Serialize(new
        {
            intent = "CARGA_EJERCICIO", confianza = 1, ejercicio = exercise,
            ejercicio_normalizado = normalized, cantidad_series = 4,
            serie = (int?)null, repeticiones = (int?)null, peso = (decimal?)null,
            unidad = (string?)null, tiempo_segundos = (int?)null, descanso_segundos = (int?)null,
            comentarios = (string?)null, observaciones = (string?)null,
            es_rm = (bool?)null, series = details, aclarar = (object?)null,
        });
        using var http = new HttpClient(new NluResponseHandler(output));
        var nlu = new GeminiNlu(http, Options.Create(new GeminiOptions { ApiKey = "test" }), NullLogger<GeminiNlu>.Instance);
        var pipeline = new CapturaTextoNlu(nlu, nlu, NullLogger<CapturaTextoNlu>.Instance);
        var analysis = await pipeline.AnalizarTextoAsync($"{exercise} 4 series", default);
        await service.AplicarTextoAsync(studentId, analysis!, now, default);
        var rows = await Rows();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(normalized, row.EjercicioCanonico);
            Assert.Null(row.Reps);
            Assert.Null(row.PesoKg);
            Assert.False(row.IsOneRepMax);
        });
    }

    [Fact]
    public async Task Capture_EmptyDetailsForUpdate_LeavesExistingValuesUntouched()
    {
        await Apply(Entities(count: 2, reps: 10, weight: 40));
        await Apply(Entities(null) with { Series = [new(1, Weight: 80), new(2)] });
        Assert.All(await Rows(), row => Assert.Equal(40, row.PesoKg));
    }

    [Theory]
    [InlineData("batchCount")]
    [InlineData("detailCount")]
    [InlineData("ordinal")]
    public async Task Capture_OutOfBoundsProviderValues_StillRejectsAllRows(string invalid)
    {
        var entities = Entities(count: 2, reps: 10, weight: 40);
        entities = invalid switch
        {
            "batchCount" => entities with { CantidadSeries = 31 },
            "detailCount" => entities with { Series = Enumerable.Range(1, 31).Select(i => new SeriesDetail(i, Weight: 50)).ToArray() },
            _ => entities with { Series = [new(0, Weight: 50)] },
        };
        await Apply(entities);
        Assert.Empty(await Rows());
    }
}
