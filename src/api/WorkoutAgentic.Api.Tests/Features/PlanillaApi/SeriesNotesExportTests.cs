using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

public sealed class SeriesNotesExportTests
{
    [Fact]
    public void Excel_ConfirmedNotes_ExportsLiteralTextAndKeepsVolumeFormula()
    {
        var plan = Plan();
        plan.Filas[0].Comments = "=1+1";
        plan.Filas[0].Observations = "Sentí un tirón en el pectoral izquierdo";
        using var stream = new MemoryStream(PlanillaArchivoFactory.Excel(plan).Contenido);
        using var zip = new ZipArchive(stream);
        using var xml = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var document = XDocument.Load(xml);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = document.Descendants(ns + "c").ToDictionary(c => (string)c.Attribute("r")!);

        Assert.Equal("Comentarios", cells["I7"].Value);
        Assert.Equal("Observaciones", cells["J7"].Value);
        Assert.Equal("inlineStr", (string?)cells["I8"].Attribute("t"));
        Assert.Equal("=1+1", cells["I8"].Value);
        Assert.Equal(plan.Filas[0].Observations, cells["J8"].Value);
        Assert.Equal("C8*D8", cells["F8"].Element(ns + "f")!.Value);
    }

    [Fact]
    public void Pdf_LongObservation_ContinuesOnPagesWithoutTruncatingEnd()
    {
        var plan = Plan();
        plan.Filas[0].Comments = "aproximación";
        plan.Filas[0].Observations = string.Join('\n', Enumerable.Repeat("Observación extensa del alumno", 90)) + "\nÚltima observación del pectoral izquierdo";
        var pdf = Encoding.ASCII.GetString(PlanillaArchivoFactory.Pdf(plan).Contenido);

        Assert.Contains("/Count 3", pdf);
        Assert.Contains(Convert.ToHexString(Encoding.Latin1.GetBytes("Comentarios: aproximación")), pdf);
        Assert.Contains(Convert.ToHexString(Encoding.Latin1.GetBytes("Última observación del pectoral izquierdo")), pdf);
        Assert.DoesNotContain("...", pdf);
    }

    private static PlanillaConfirmada Plan() => new()
    {
        Id = 1, ConfirmadaUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
        Filas = [new PlanillaFila { Orden = 1, EjercicioCanonical = "Press de banca", NroSerie = 2, Reps = 10, PesoKg = 40, Unidad = Unidad.Kg }],
    };
}
