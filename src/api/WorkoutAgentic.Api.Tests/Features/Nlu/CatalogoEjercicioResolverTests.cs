using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

// Determinístico y auditable: fixtures del spec NLU (docs/domain/nlu-captura.md)
// contra el catálogo del seed (mismos ids: orden 1..10 de CatalogoEjercicioSeed).
public class CatalogoEjercicioResolverTests
{
    private static IReadOnlyList<CatalogoEjercicio> Catalogo() =>
        CatalogoEjercicioSeed.Crear()
            .Select((e, i) => new CatalogoEjercicio(
                Id: i + 1,
                NombreCanonico: e.NombreCanonico,
                Apodos: e.Apodos,
                GrupoMuscular: e.GrupoMuscular,
                UsaPeso: e.UsaPeso,
                UsaTiempo: e.UsaTiempo))
            .ToList();

    [Theory]
    [InlineData("sillón", "Sillón de cuadríceps")]
    [InlineData("sillon", "Sillón de cuadríceps")]                   // sin tilde
    [InlineData("sillón de cuadríceps", "Sillón de cuadríceps")]     // canónico exacto
    [InlineData("prensa", "Prensa de pierna")]
    [InlineData("remos", "Remo con mancuernas")]
    [InlineData("remo con pesas", "Remo con mancuernas")]            // apodo
    [InlineData("dominadas", "Dominadas")]
    [InlineData("plancha", "Plancha")]
    [InlineData("tengo el sillón en la máquina", "Sillón de cuadríceps")] // contención
    public void Matcheado_PorApodoOCanónico(string query, string esperado)
    {
        var resultado = CatalogoEjercicioResolver.Resolver(query, Catalogo());

        Assert.Equal(EstadoResolucion.Matcheado, resultado.Estado);
        Assert.Equal(esperado, resultado.EjercicioCanonico);
        Assert.NotNull(resultado.EjercicioId);
    }

    [Theory]
    [InlineData("el pecho")]   // NLU §2: "el pecho" → preguntar cuál del grupo
    [InlineData("pecho")]
    [InlineData("pierna")]
    public void GrupoMuscularConVariosEjercicios_Ambiguo(string query)
    {
        var resultado = CatalogoEjercicioResolver.Resolver(query, Catalogo());

        Assert.Equal(EstadoResolucion.Ambiguo, resultado.Estado);
        Assert.Null(resultado.EjercicioId);
        Assert.True(resultado.Sugerencias.Count >= 2, "Deben sugerirse los ejercicios del grupo.");
    }

    [Fact]
    public void GrupoPecho_SugierePressYCruces()
    {
        var resultado = CatalogoEjercicioResolver.Resolver("el pecho", Catalogo());

        var nombres = resultado.Sugerencias.Select(s => s.NombreCanonico).ToList();
        Assert.Contains("Press de pecho en máquina", nombres);
        Assert.Contains("Cruces en polea", nombres);
    }

    [Fact]
    public void GrupoConUnSoloEjercicio_Matcheado()
    {
        var resultado = CatalogoEjercicioResolver.Resolver("abdomen", Catalogo());

        Assert.Equal(EstadoResolucion.Matcheado, resultado.Estado);
        Assert.Equal("Plancha", resultado.EjercicioCanonico);
    }

    [Fact]
    public void CercaPeroDebajoDelUmbral_SinMatchConSugerencias()
    {
        // "sillom": un solo carácter fuera → score 0.833 < 0.85 ([SUPUESTO ADR-0003]).
        var resultado = CatalogoEjercicioResolver.Resolver("sillom", Catalogo());

        Assert.Equal(EstadoResolucion.SinMatch, resultado.Estado);
        Assert.Null(resultado.EjercicioId);
        var primera = Assert.Single(resultado.Sugerencias);
        Assert.Equal("Sillón de cuadríceps", primera.NombreCanonico);
    }

    [Fact]
    public void FaroDelCatalogo_SinMatchSinSugerencias() =>
        AssertSinMatchSinSugerencias("hola");

    private static void AssertSinMatchSinSugerencias(string query)
    {
        var resultado = CatalogoEjercicioResolver.Resolver(query, Catalogo());

        Assert.Equal(EstadoResolucion.SinMatch, resultado.Estado);
        Assert.Null(resultado.EjercicioId);
        Assert.Empty(resultado.Sugerencias);
    }

    [Fact]
    public void Vacio_SinMatch() =>
        AssertSinMatchSinSugerencias("   ");
}
