using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

public sealed class OneRepMaxExportTests
{
    [Fact]
    public void Progress_WithoutNumericLoad_KeepsReferenceWithoutInventingPercentage()
    {
        var plan = Example();
        plan.Filas[0].PesoKg = null;
        plan.Filas[0].Unidad = Unidad.PesoCorporal;
        var progress = OneRepMaxProgress.Summarize(plan)[0];
        Assert.Null(progress.SessionMaxKg);
        Assert.Null(progress.Percentage);
        Assert.Contains("24/11/2026", progress.Description);
        Assert.Contains("planilla #12, fila 4", progress.Description);
        Assert.Contains("Sin carga numérica", progress.Description);
    }

    [Fact]
    public void Excel_FrozenReference_ContainsNumericComparisonAndOneRepMaxMark()
    {
        var plan = Example();
        var file = PlanillaArchivoFactory.Excel(plan);
        using var zip = new ZipArchive(new MemoryStream(file.Contenido));
        using var sheet = zip.GetEntry("xl/worksheets/sheet2.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = xml.Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);
        Assert.Equal("100", cells["B5"].Value);
        Assert.Equal("170", cells["C5"].Value);
        Assert.Equal("24/11/2026", cells["D5"].Value);
        Assert.Equal("ROUND(B5/C5*100,1)", cells["E5"].Element(ns + "f")!.Value);
        Assert.Equal("58.8", cells["E5"].Element(ns + "v")!.Value);
        Assert.Equal("12", cells["F5"].Value);
        Assert.Contains("Sin RM previo", cells["H6"].Value);
        using var firstSheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        Assert.Contains("RM / 1 repetición", XDocument.Load(firstSheet).ToString());
        using var workbook = zip.GetEntry("xl/workbook.xml")!.Open();
        Assert.Contains("Progreso RM", XDocument.Load(workbook).ToString());
        SaveForVisualReview("rm-progress.xlsx", file.Contenido);
    }

    [Fact]
    public void Pdf_FrozenReference_ContainsPercentageDateOriginAndDeclaredMark()
    {
        var file = PlanillaArchivoFactory.Pdf(Example());
        var pdf = Encoding.ASCII.GetString(file.Contenido);
        var content = string.Join(' ', Regex.Matches(pdf, @"<([0-9A-F]+)> Tj")
            .Select(m => Encoding.Latin1.GetString(Convert.FromHexString(m.Groups[1].Value))));
        Assert.Contains("58,8 %", content);
        Assert.Contains("24/11/2026", content);
        Assert.Contains("planilla #12, fila 4", content);
        Assert.Contains("RM declarado / 1 repetición", content);
        Assert.Contains("Sin RM previo", content);
        Assert.Contains("por sí solo no indica mejora", content);
        SaveForVisualReview("rm-progress.pdf", file.Contenido);
    }

    private static void SaveForVisualReview(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("WA_EXPORT_REVIEW_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    private static PlanillaConfirmada Example() => new()
    {
        Id = 13, ConfirmadaUtc = new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc),
        Filas =
        [
            new() { Orden = 1, EjercicioCanonical = "Press de banca", NroSerie = 4, Reps = 6, PesoKg = 100, Unidad = Unidad.Kg,
                Comments = "efectiva", ReferenceOneRepMaxKg = 170, ReferenceConfirmedUtc = new DateTime(2026, 11, 24, 17, 0, 0, DateTimeKind.Utc),
                ReferencePlanillaId = 12, ReferenceRowOrder = 4 },
            new() { Orden = 2, EjercicioCanonical = "Peso muerto", NroSerie = 1, Reps = 1, PesoKg = 140, Unidad = Unidad.Kg, IsOneRepMax = true },
        ],
    };
}
