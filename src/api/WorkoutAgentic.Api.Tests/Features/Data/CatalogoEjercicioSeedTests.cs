using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Tests.Features.Data;

// El DbInitializer siembra el catálogo mínimo (spec NLU fixtures) SÓLO si la
// tabla está vacía: una base con datos no se repiembra (idempotente).
public class CatalogoEjercicioSeedTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-seed-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program>? _factory;

    [Fact]
    public async Task BaseNueva_SiembraElCatalogoMinimo()
    {
        using var scope = CrearFactory(_dbPath).Services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();

        Assert.Equal(CatalogoEjercicioSeed.Crear().Count, await db.Ejercicios.CountAsync());

        var sillon = await db.Ejercicios.SingleAsync(e => e.NombreCanonico == "Sillón de cuadríceps");
        Assert.Contains("sillón", sillon.Apodos);
        Assert.True(sillon.UsaPeso);

        var plancha = await db.Ejercicios.SingleAsync(e => e.NombreCanonico == "Plancha");
        Assert.True(plancha.UsaTiempo);
        Assert.False(plancha.UsaPeso);
    }

    [Fact]
    public async Task BaseConCatalogo_NoSeRepiembra()
    {
        CrearFactory(_dbPath).Dispose();   // primera vez: siembra
        _factory = null;

        using var scope = CrearFactory(_dbPath).Services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();

        Assert.Equal(CatalogoEjercicioSeed.Crear().Count, await db.Ejercicios.CountAsync());
    }

    private WebApplicationFactory<Program> CrearFactory(string dbPath)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
                })));
        return _factory;
    }

    public void Dispose()
    {
        _factory?.Dispose();

        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // best effort: el archivo queda en %TEMP%.
                }
            }
        }
    }
}
