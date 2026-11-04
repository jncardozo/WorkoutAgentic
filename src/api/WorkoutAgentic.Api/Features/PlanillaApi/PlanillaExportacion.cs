using System.Globalization;
using System.IO.Compression;
using System.Net.Mail;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Features.PlanillaApi;

internal sealed record PlanillaArchivo(byte[] Contenido, string Nombre, string TipoContenido);

internal sealed record ResumenReporte(int Series, decimal? VolumenKgReps, decimal? CargaMaximaKg);

internal static class PlanillaArchivoFactory
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    internal const string TipoPdf = "application/pdf";

    private static ResumenReporte Resumir(PlanillaConfirmada planilla)
    {
        var filas = planilla.Filas;
        var volumen = filas
            .Where(f => f.Reps.HasValue && TieneCargaNumerica(f))
            .Sum(f => f.Reps!.Value * f.PesoKg!.Value);
        var tieneVolumen = filas.Any(f => f.Reps.HasValue && TieneCargaNumerica(f));
        var carga = filas
            .Where(TieneCargaNumerica)
            .Select(f => f.PesoKg!.Value)
            .DefaultIfEmpty()
            .Max();
        var tieneCarga = filas.Any(TieneCargaNumerica);
        return new(filas.Count, tieneVolumen ? volumen : null, tieneCarga ? carga : null);
    }

    private static bool TieneCargaNumerica(PlanillaFila fila) =>
        fila.PesoKg.HasValue && fila.Unidad is not Unidad.PesoCorporal and not Unidad.Tiempo;

    public static PlanillaArchivo Excel(PlanillaConfirmada planilla)
    {
        using var contenido = new MemoryStream();
        using (var zip = new ZipArchive(contenido, ZipArchiveMode.Create, leaveOpen: true))
        {
            Agregar(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Agregar(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Agregar(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Reporte" sheetId="1" r:id="rId1"/></sheets><calcPr calcId="191029" fullCalcOnLoad="1" forceFullCalc="1"/></workbook>
                """);
            Agregar(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            Agregar(zip, "xl/styles.xml", EstilosExcel);
            AgregarHoja(zip, planilla);
        }

        return new(contenido.ToArray(), $"planilla-{planilla.Id}.xlsx", TipoExcel);
    }

    public static PlanillaArchivo Pdf(PlanillaConfirmada planilla)
    {
        return new(CrearPdfReporte(planilla), $"planilla-{planilla.Id}.pdf", TipoPdf);
    }

    private static void AgregarHoja(ZipArchive zip, PlanillaConfirmada planilla)
    {
        var entrada = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var stream = entrada.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            CloseOutput = false,
        });

        writer.WriteStartDocument(true);
        writer.WriteStartElement("worksheet", SpreadsheetNs);
        writer.WriteStartElement("sheetPr", SpreadsheetNs);
        writer.WriteStartElement("pageSetUpPr", SpreadsheetNs);
        writer.WriteAttributeString("fitToPage", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", SpreadsheetNs);
        writer.WriteStartElement("sheetView", SpreadsheetNs);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNs);
        writer.WriteAttributeString("ySplit", "7");
        writer.WriteAttributeString("topLeftCell", "A8");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", SpreadsheetNs);
        writer.WriteAttributeString("defaultRowHeight", "20");
        writer.WriteEndElement();
        writer.WriteStartElement("cols", SpreadsheetNs);
        EscribirAnchoColumna(writer, 1, 38);
        EscribirAnchoColumna(writer, 2, 10);
        EscribirAnchoColumna(writer, 3, 16);
        EscribirAnchoColumna(writer, 4, 14);
        EscribirAnchoColumna(writer, 5, 18);
        EscribirAnchoColumna(writer, 6, 22);
        EscribirAnchoColumna(writer, 7, 18);
        EscribirAnchoColumna(writer, 8, 25);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNs);
        EscribirFilaConEstilo(writer, 1, ["WORKOUTAGENTIC  |  REPORTE DE ENTRENAMIENTO"], 1);
        EscribirFilaConEstilo(writer, 2, [$"Planilla confirmada #{planilla.Id}  |  {planilla.ConfirmadaUtc.ToLocalTime():dd/MM/yyyy HH:mm}"], 2);
        EscribirFilaConEstilo(writer, 4, ["Series registradas", "", "Volumen calculable (kg x reps)", "", "Carga máxima (kg)", ""], 3);
        var resumen = Resumir(planilla);
        EscribirFilaConEstilo(writer, 5, [resumen.Series.ToString(CultureInfo.InvariantCulture), "", resumen.VolumenKgReps?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Sin datos", "", resumen.CargaMaximaKg?.ToString("0.##", CultureInfo.InvariantCulture) ?? "Sin datos", ""], 4);
        EscribirFilaConEstilo(writer, 6, ["El volumen suma repeticiones por carga numérica; excluye peso corporal y datos faltantes."], 5);
        EscribirFilaConEstilo(writer, 7, ["Ejercicio", "Serie", "Repeticiones", "Peso (kg)", "Unidad", "Volumen (kg x reps)", "Estado", "Progresión"], 6);

        var indice = 8;
        foreach (var fila in planilla.Filas.OrderBy(f => f.Orden))
        {
            writer.WriteStartElement("row", SpreadsheetNs);
            writer.WriteAttributeString("r", indice.ToString(CultureInfo.InvariantCulture));
            var estilo = indice % 2 == 0 ? 7 : 8;
            CeldaTexto(writer, $"A{indice}", fila.EjercicioCanonical, estilo);
            CeldaNumero(writer, $"B{indice}", fila.NroSerie.ToString(CultureInfo.InvariantCulture), estilo);
            CeldaNumero(writer, $"C{indice}", fila.Reps?.ToString(CultureInfo.InvariantCulture), estilo);
            CeldaNumero(writer, $"D{indice}", fila.PesoKg?.ToString(CultureInfo.InvariantCulture), estilo);
            CeldaTexto(writer, $"E{indice}", fila.Unidad?.ToString() ?? "—", estilo);
            if (fila.Reps.HasValue && TieneCargaNumerica(fila))
            {
                var volumen = fila.Reps.Value * fila.PesoKg!.Value;
                CeldaFormula(writer, $"F{indice}", $"C{indice}*D{indice}", volumen, estilo);
            }
            else
            {
                CeldaTexto(writer, $"F{indice}", "—", estilo);
            }
            CeldaTexto(writer, $"G{indice}", fila.Incompleta ? "Incompleta" : "Registrada", fila.Incompleta ? 9 : estilo);
            CeldaTexto(writer, $"H{indice}", BarraProgresion(fila.PesoKg, fila.Unidad, resumen.CargaMaximaKg), estilo);
            writer.WriteEndElement();
            indice++;
        }

        writer.WriteStartElement("row", SpreadsheetNs);
        writer.WriteAttributeString("r", indice.ToString(CultureInfo.InvariantCulture));
        CeldaTexto(writer, $"A{indice}", "TOTAL DE VOLUMEN CALCULABLE", 10);
        if (resumen.VolumenKgReps.HasValue)
        {
            CeldaFormula(writer, $"F{indice}", $"SUM(F8:F{indice - 1})", resumen.VolumenKgReps.Value, 10);
        }
        else
        {
            CeldaTexto(writer, $"F{indice}", "Sin datos", 10);
        }
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNs);
        writer.WriteAttributeString("ref", $"A7:H{Math.Max(7, indice - 1)}");
        writer.WriteEndElement();
        writer.WriteStartElement("mergeCells", SpreadsheetNs);
        writer.WriteAttributeString("count", "9");
        EscribirCeldaFusionada(writer, "A1:H1");
        EscribirCeldaFusionada(writer, "A2:H2");
        EscribirCeldaFusionada(writer, "A4:B4");
        EscribirCeldaFusionada(writer, "C4:D4");
        EscribirCeldaFusionada(writer, "E4:F4");
        EscribirCeldaFusionada(writer, "A5:B5");
        EscribirCeldaFusionada(writer, "C5:D5");
        EscribirCeldaFusionada(writer, "E5:F5");
        EscribirCeldaFusionada(writer, "A6:H6");
        writer.WriteEndElement();
        writer.WriteStartElement("pageMargins", SpreadsheetNs);
        writer.WriteAttributeString("left", "0.25");
        writer.WriteAttributeString("right", "0.25");
        writer.WriteAttributeString("top", "0.5");
        writer.WriteAttributeString("bottom", "0.5");
        writer.WriteAttributeString("header", "0.2");
        writer.WriteAttributeString("footer", "0.2");
        writer.WriteEndElement();
        writer.WriteStartElement("pageSetup", SpreadsheetNs);
        writer.WriteAttributeString("paperSize", "9");
        writer.WriteAttributeString("orientation", "landscape");
        writer.WriteAttributeString("fitToWidth", "1");
        writer.WriteAttributeString("fitToHeight", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void EscribirAnchoColumna(XmlWriter writer, int indice, int ancho)
    {
        writer.WriteStartElement("col", SpreadsheetNs);
        writer.WriteAttributeString("min", indice.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", indice.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", ancho.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void EscribirCeldaFusionada(XmlWriter writer, string referencia)
    {
        writer.WriteStartElement("mergeCell", SpreadsheetNs);
        writer.WriteAttributeString("ref", referencia);
        writer.WriteEndElement();
    }

    private static string BarraProgresion(decimal? cargaKg, Unidad? unidad, decimal? maximo)
    {
        if (!cargaKg.HasValue || !maximo.HasValue || maximo <= 0 || unidad is Unidad.PesoCorporal or Unidad.Tiempo)
        {
            return unidad switch
            {
                Unidad.PesoCorporal => "Al cuerpo",
                Unidad.Tiempo => "Tiempo",
                _ => "Sin dato",
            };
        }

        var porcentaje = Math.Clamp((int)Math.Round(cargaKg.Value / maximo.Value * 100m), 0, 100);
        var completas = (porcentaje + 9) / 10;
        return $"{new string('■', completas)}{new string('·', 10 - completas)} {porcentaje}%";
    }

    private static void EscribirFilaConEstilo(XmlWriter writer, int indice, IReadOnlyList<string> valores, int estilo)
    {
        writer.WriteStartElement("row", SpreadsheetNs);
        writer.WriteAttributeString("r", indice.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < valores.Count; i++)
        {
            CeldaTexto(writer, $"{Columna(i + 1)}{indice}", valores[i], estilo);
        }
        writer.WriteEndElement();
    }

    private const string EstilosExcel = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="5"><font><sz val="11"/><name val="Aptos"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="16"/><name val="Aptos Display"/></font><font><color rgb="FF475569"/><sz val="10"/><name val="Aptos"/></font><font><b/><color rgb="FF047857"/><sz val="10"/><name val="Aptos"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="10"/><name val="Aptos"/></font></fonts>
          <fills count="6"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF047857"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE8F5EC"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEFF2FF"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFF4D6"/><bgColor indexed="64"/></patternFill></fill></fills>
          <borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FFD5DDE8"/></left><right style="thin"><color rgb="FFD5DDE8"/></right><top style="thin"><color rgb="FFD5DDE8"/></top><bottom style="thin"><color rgb="FFD5DDE8"/></bottom><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="11"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="4" borderId="0" xfId="0"/><xf numFmtId="0" fontId="3" fillId="3" borderId="0" xfId="0"/><xf numFmtId="0" fontId="3" fillId="3" borderId="1" xfId="0"/><xf numFmtId="0" fontId="2" fillId="4" borderId="1" xfId="0"/><xf numFmtId="0" fontId="4" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0"/><xf numFmtId="0" fontId="0" fillId="4" borderId="1" xfId="0"/><xf numFmtId="0" fontId="3" fillId="5" borderId="1" xfId="0"/><xf numFmtId="0" fontId="4" fillId="3" borderId="1" xfId="0"/></cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;

    private static void CeldaTexto(XmlWriter writer, string referencia, string? valor, int estilo = 0)
    {
        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", referencia);
        if (estilo > 0) writer.WriteAttributeString("s", estilo.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is", SpreadsheetNs);
        writer.WriteStartElement("t", SpreadsheetNs);
        writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        writer.WriteString(valor ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void CeldaNumero(XmlWriter writer, string referencia, string? valor, int estilo = 0)
    {
        if (valor is null)
        {
            return;
        }

        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", referencia);
        if (estilo > 0) writer.WriteAttributeString("s", estilo.ToString(CultureInfo.InvariantCulture));
        writer.WriteElementString("v", SpreadsheetNs, valor);
        writer.WriteEndElement();
    }

    private static void CeldaFormula(XmlWriter writer, string referencia, string formula, decimal valor, int estilo)
    {
        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", referencia);
        writer.WriteAttributeString("s", estilo.ToString(CultureInfo.InvariantCulture));
        writer.WriteElementString("f", SpreadsheetNs, formula);
        writer.WriteElementString("v", SpreadsheetNs, valor.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static string Columna(int indice)
    {
        var resultado = string.Empty;
        while (indice > 0)
        {
            indice--;
            resultado = (char)('A' + (indice % 26)) + resultado;
            indice /= 26;
        }
        return resultado;
    }

    private static byte[] CrearPdfReporte(PlanillaConfirmada planilla)
    {
        var filas = planilla.Filas.OrderBy(f => f.Orden).ToArray();
        var paginas = filas.Chunk(20).Select(p => p.ToArray()).ToArray();
        if (paginas.Length == 0) paginas = [[]];
        var resumen = Resumir(planilla);
        var contenidos = paginas.Select((pagina, indice) => ContenidoPaginaPdf(planilla, resumen, pagina, indice + 1, paginas.Length)).ToArray();
        var objetos = new List<byte[]>
        {
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
        };
        var idsPagina = new List<int>();
        foreach (var contenido in contenidos)
        {
            var idPagina = objetos.Count + 1;
            var idContenido = idPagina + 1;
            idsPagina.Add(idPagina);
            objetos.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {idContenido} 0 R >>"));
            objetos.Add(Ascii($"<< /Length {contenido.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n{Encoding.ASCII.GetString(contenido)}\nendstream"));
        }
        objetos[0] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objetos[1] = Ascii($"<< /Type /Pages /Kids [{string.Join(' ', idsPagina.Select(id => $"{id} 0 R"))}] /Count {paginas.Length.ToString(CultureInfo.InvariantCulture)} >>");

        using var salida = new MemoryStream();
        Escribir(salida, Ascii("%PDF-1.4\n"));
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objetos.Count; i++)
        {
            offsets.Add(salida.Position);
            Escribir(salida, Ascii($"{i + 1} 0 obj\n"));
            Escribir(salida, objetos[i]);
            Escribir(salida, Ascii("\nendobj\n"));
        }
        var inicioXref = salida.Position;
        Escribir(salida, Ascii($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1))
        {
            Escribir(salida, Ascii($"{offset.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n"));
        }
        Escribir(salida, Ascii($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref.ToString(CultureInfo.InvariantCulture)}\n%%EOF"));
        return salida.ToArray();
    }

    private static byte[] ContenidoPaginaPdf(
        PlanillaConfirmada planilla,
        ResumenReporte resumen,
        IReadOnlyList<PlanillaFila> filas,
        int numeroPagina,
        int totalPaginas)
    {
        var pdf = new StringBuilder();
        RectanguloPdf(pdf, 0, 777, 595, 65, "0.02 0.38 0.20");
        TextoPdf(pdf, 40, 814, 17, "WORKOUTAGENTIC  |  REPORTE DE ENTRENAMIENTO", "1 1 1");
        TextoPdf(pdf, 40, 790, 9, $"PLANILLA CONFIRMADA #{planilla.Id}    {planilla.ConfirmadaUtc.ToLocalTime():dd/MM/yyyy HH:mm}", "0.87 0.98 0.91");

        var cultura = CultureInfo.GetCultureInfo("es-AR");
        var volumenTexto = resumen.VolumenKgReps?.ToString("N2", cultura) ?? "Sin datos";
        var picoTexto = resumen.CargaMaximaKg?.ToString("0.##", cultura) ?? "Sin datos";
        TarjetaPdf(pdf, 40, 700, 160, "SERIES REGISTRADAS", resumen.Series.ToString(CultureInfo.InvariantCulture), "filas confirmadas");
        TarjetaPdf(pdf, 217, 700, 160, "VOLUMEN CALCULABLE", volumenTexto, "kg x repeticiones");
        TarjetaPdf(pdf, 394, 700, 160, "CARGA MÁXIMA", picoTexto, "kg; cargas numéricas");
        TextoPdf(pdf, 40, 685, 7, "El volumen excluye peso corporal y filas con datos faltantes.", "0.35 0.39 0.43");

        RectanguloPdf(pdf, 40, 576, 515, 91, "0.95 0.97 1");
        TextoPdf(pdf, 52, 650, 10, "PROGRESIÓN DE CARGA POR SERIE", "0.08 0.12 0.20");
        var graficables = filas.Where(TieneCargaNumerica).ToArray();
        if (graficables.Length == 0)
        {
            TextoPdf(pdf, 52, 615, 8, "No hay cargas numéricas para graficar en esta página.", "0.35 0.39 0.43");
        }
        else
        {
            var maximo = Math.Max(1m, graficables.Max(f => f.PesoKg!.Value));
            var paso = 470d / Math.Max(1, graficables.Length);
            for (var i = 0; i < graficables.Length; i++)
            {
                var altura = 43d * (double)(graficables[i].PesoKg!.Value / maximo);
                var x = 58 + paso * i;
                RectanguloPdf(pdf, x, 594, Math.Max(8, paso - 7), altura, i == graficables.Length - 1 ? "0.02 0.38 0.20" : "0.30 0.80 0.46");
                TextoPdf(pdf, x, 583, 6, $"S{graficables[i].NroSerie}", "0.20 0.25 0.31");
                TextoPdf(pdf, x, 594 + altura + 3, 6, graficables[i].PesoKg!.Value.ToString("0.#", cultura), "0.08 0.12 0.20");
            }
        }

        RectanguloPdf(pdf, 40, 535, 515, 24, "0.89 0.91 0.99");
        TextoPdf(pdf, 48, 544, 8, "EJERCICIO", "0.12 0.18 0.27");
        TextoPdf(pdf, 320, 544, 8, "SERIE", "0.12 0.18 0.27");
        TextoPdf(pdf, 365, 544, 8, "REPS", "0.12 0.18 0.27");
        TextoPdf(pdf, 410, 544, 8, "PESO / TIEMPO", "0.12 0.18 0.27");
        TextoPdf(pdf, 478, 544, 8, "ESTADO", "0.12 0.18 0.27");

        var y = 513d;
        for (var i = 0; i < filas.Count; i++)
        {
            if (i % 2 == 0) RectanguloPdf(pdf, 40, y - 5, 515, 20, "0.97 0.98 1");
            var fila = filas[i];
            TextoPdf(pdf, 48, y, 8, Acortar(fila.EjercicioCanonical, 43), "0.08 0.12 0.20");
            TextoPdf(pdf, 323, y, 8, fila.NroSerie.ToString(CultureInfo.InvariantCulture), "0.08 0.12 0.20");
            TextoPdf(pdf, 368, y, 8, fila.Reps?.ToString(CultureInfo.InvariantCulture) ?? "-", "0.08 0.12 0.20");
            var peso = fila.Unidad == Unidad.PesoCorporal
                ? "Al cuerpo"
                : fila.Unidad == Unidad.Tiempo && fila.TiempoSeg.HasValue
                    ? $"{fila.TiempoSeg.Value} s"
                    : TieneCargaNumerica(fila) ? $"{fila.PesoKg!.Value.ToString("0.##", cultura)} kg" : "-";
            TextoPdf(pdf, 410, y, 8, peso, "0.02 0.38 0.20");
            TextoPdf(pdf, 478, y, 7, fila.Incompleta ? "INCOMPLETA" : "REGISTRADA", fila.Incompleta ? "0.65 0.35 0.02" : "0.02 0.38 0.20");
            y -= 20;
        }
        RectanguloPdf(pdf, 40, 40, 515, 1, "0.83 0.86 0.90");
        TextoPdf(pdf, 40, 26, 7, $"WorkoutAgentic  |  Planilla #{planilla.Id}", "0.35 0.39 0.43");
        TextoPdf(pdf, 478, 26, 7, $"Página {numeroPagina} de {totalPaginas}", "0.35 0.39 0.43");
        return Ascii(pdf.ToString());
    }

    private static void TarjetaPdf(StringBuilder pdf, double x, double y, double width, string etiqueta, string valor, string nota)
    {
        RectanguloPdf(pdf, x, y, width, 58, "0.95 0.97 1");
        TextoPdf(pdf, x + 9, y + 43, 7, etiqueta, "0.25 0.31 0.37");
        TextoPdf(pdf, x + 9, y + 23, 15, valor, "0.02 0.38 0.20");
        TextoPdf(pdf, x + 9, y + 8, 6, nota, "0.35 0.39 0.43");
    }

    private static void RectanguloPdf(StringBuilder pdf, double x, double y, double width, double height, string color) =>
        pdf.Append(color).Append(" rg ").Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(width.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(height.ToString("0.##", CultureInfo.InvariantCulture)).AppendLine(" re f");

    private static void TextoPdf(StringBuilder pdf, double x, double y, int size, string texto, string color) =>
        pdf.Append(color).Append(" rg BT /F1 ").Append(size.ToString(CultureInfo.InvariantCulture)).Append(" Tf ")
            .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Td <")
            .Append(Convert.ToHexString(Encoding.Latin1.GetBytes(texto))).AppendLine("> Tj ET");

    private static string Acortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..(maximo - 3)] + "...";

    private static void Agregar(ZipArchive zip, string ruta, string contenido)
    {
        var entrada = zip.CreateEntry(ruta, CompressionLevel.Optimal);
        using var stream = entrada.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contenido.Trim());
    }

    private static byte[] Ascii(string texto) => Encoding.ASCII.GetBytes(texto);

    private static void Escribir(Stream destino, byte[] bytes) => destino.Write(bytes, 0, bytes.Length);
}

public sealed class CorreoSmtpOptions
{
    public const string SectionName = "CorreoSmtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "WorkoutAgentic";
}

internal enum EstadoEnvioCorreo
{
    Enviado,
    ConfiguracionIncompleta,
    Fallo,
}

internal sealed class PlanillaCorreoSender(
    IOptions<CorreoSmtpOptions> options,
    ILogger<PlanillaCorreoSender> logger)
{
    public async Task<EstadoEnvioCorreo> EnviarAsync(
        string destinatario,
        PlanillaArchivo archivo,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Host)
            || config.Port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(config.FromAddress))
        {
            return EstadoEnvioCorreo.ConfiguracionIncompleta;
        }

        try
        {
            using var mensaje = new MailMessage
            {
                From = new MailAddress(config.FromAddress, config.FromName),
                Subject = "Tu planilla de entrenamiento",
                Body = "Adjuntamos la planilla confirmada desde WorkoutAgentic.",
            };
            mensaje.To.Add(new MailAddress(destinatario));
            mensaje.Attachments.Add(new Attachment(
                new MemoryStream(archivo.Contenido), archivo.Nombre, archivo.TipoContenido));

            using var cliente = new SmtpClient(config.Host, config.Port)
            {
                EnableSsl = config.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
            };
            if (!string.IsNullOrWhiteSpace(config.UserName))
            {
                cliente.Credentials = new System.Net.NetworkCredential(config.UserName, config.Password);
            }

            await cliente.SendMailAsync(mensaje, cancellationToken);
            return EstadoEnvioCorreo.Enviado;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Falló el envío de la planilla por correo.");
            return EstadoEnvioCorreo.Fallo;
        }
    }
}

internal sealed record ComandoEnviarPlanillaCorreo(int AlumnoId, string Correo, string Formato);

internal sealed record RespuestaEnviarPlanillaCorreo(string Mensaje);
