using System.Globalization;
using WorkoutAgentic.Api.Features.CapturaSesion;

namespace WorkoutAgentic.Api.Tests.Features.CapturaSesion;

// Parser determinístico de respuestas a aclaraciones: puro texto, sin LLM.
// "null" significa que NO se entendió (reintento), nunca un valor default.
public class ParseadorAclaracionesTests
{
    private static decimal? ParsearPeso(string? texto) => ParseadorAclaraciones.ParsearPeso(texto);

    private static void AfirmarPeso(string texto, string esperado)
    {
        var valor = string.IsNullOrEmpty(esperado)
            ? (decimal?)null
            : decimal.Parse(esperado, CultureInfo.InvariantCulture);
        Assert.Equal(valor, ParsearPeso(texto));
    }

    [Theory]
    [InlineData("50", "50")]
    [InlineData(" 50 ", "50")]
    [InlineData("50,5", "50.5")]
    [InlineData("50.5", "50.5")]
    [InlineData("50 y medio", "50.5")]
    [InlineData("50 y media", "50.5")]
    [InlineData("50 kilos", "50")]
    [InlineData("50 kilos y medio", "50.5")]
    [InlineData("50 kg", "50")]
    [InlineData("50k", "50")]
    [InlineData("kilos y medio", "1.5")]
    [InlineData("un kilo y medio", "1.5")]
    public void ParsearPeso_AceptaRespuestasConocidas(string texto, string esperado)
    {
        AfirmarPeso(texto, esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no sé")]
    [InlineData("cincuenta")]
    [InlineData("50 y pico")]
    [InlineData("unos 40")]
    [InlineData("pecho")]
    [InlineData("50 y un cuarto")]
    public void ParsearPeso_RechazaOtrasRespuestas(string? texto)
    {
        Assert.Null(ParsearPeso(texto));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData(" 12 ")]
    public void ParsearEntero_AceptaDigitosPuros(string texto)
    {
        var esperado = int.Parse(texto.Trim(), CultureInfo.InvariantCulture);
        Assert.Equal(esperado, ParseadorAclaraciones.ParsearEntero(texto));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("dos")]
    [InlineData("segunda")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("12 con 40")]
    [InlineData("0")]
    public void ParsearEntero_RechazaOtrasRespuestas(string? texto)
    {
        Assert.Null(ParseadorAclaraciones.ParsearEntero(texto));
    }

    [Theory]
    [InlineData("otro", true)]
    [InlineData("OTRO", true)]
    [InlineData("otra", true)]
    [InlineData("otro ejercicio", false)]
    [InlineData("nada", false)]
    [InlineData("", false)]
    public void EsOtro_SoloLaPalabraSuelta(string texto, bool esperado)
    {
        Assert.Equal(esperado, ParseadorAclaraciones.EsOtro(texto));
    }

    [Theory]
    [InlineData("seguir", true)]
    [InlineData("SEGUIR", true)]
    [InlineData("seguimos", false)]
    [InlineData("se guir", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsSeguir_SoloLaPalabraExacta(string? texto, bool esperado)
    {
        Assert.Equal(esperado, ParseadorAclaraciones.EsSeguir(texto));
    }

    [Theory]
    [InlineData("Sillón", "sillon")]
    [InlineData(" 12 con 40 ", "12 con 40")]
    [InlineData("12,5", "12,5")]
    public void Normalizar_QuitaAcentosYColapsaEspacios(string texto, string esperado)
    {
        Assert.Equal(esperado, ParseadorAclaraciones.Normalizar(texto));
    }
}
