using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.Alumnos;

// Tests del alta de alumnos (ADR-0010): WebApplicationFactory + BD efímera.
// Nada de WhatsApp/Gemini: el alta es dominio puro sobre SQLite.
public class AlumnosEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-altas-{Guid.NewGuid():N}.db");

    public AlumnosEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
        });
    }

    public void Dispose()
    {
        _factory.Dispose();

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

    [Fact]
    public async Task Alta_WithNombreYTelefono_Returns201AndPersists()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/alumnos",
            new { nombre = "Lucía Fernández", telefonoWa = "+54 9 11 5555-1234" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var alta = await response.Content.ReadFromJsonAsync<AlumnoVista>(JsonOptions);
        Assert.NotNull(alta);
        Assert.Equal("Lucía Fernández", alta!.Nombre);
        // Canonizado como el canal: solo dígitos.
        Assert.Equal("5491155551234", alta.TelefonoWa);

        var alumnoDb = await ConDbAsync(async db =>
            await db.Alumnos.AsNoTracking().SingleOrDefaultAsync(a => a.Nombre == "Lucía Fernández"));
        Assert.NotNull(alumnoDb);
        Assert.Equal("ACTIVO", alumnoDb!.Estado);
    }

    [Fact]
    public async Task Alta_WithoutTelefono_PermitesVariosAlumnos()
    {
        var primera = await Client().PostAsJsonAsync("/api/alumnos", new { nombre = "A" }, JsonOptions);
        var segunda = await Client().PostAsJsonAsync("/api/alumnos", new { nombre = "B" }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        var alumnos = await ConDbAsync(async db => await db.Alumnos.AsNoTracking().CountAsync());
        Assert.Equal(2, alumnos);
        // Ambos viven: sin teléfono NO chocan contra el índice único.
        Assert.Equal(0, await ConDbAsync(async db =>
            await db.Alumnos.AsNoTracking().CountAsync(a => a.TelefonoWa != null && a.TelefonoWa != "")));
    }

    [Fact]
    public async Task Alta_WithEmptyNombre_Returns400Problem()
    {
        var response = await Client().PostAsJsonAsync("/api/alumnos", new { nombre = "   " }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Nombre requerido", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Alta_WithInvalidTelefono_Returns400Problem()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/alumnos",
            new { nombre = "Nadia", telefonoWa = "abc-12" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Teléfono inválido", problem.GetProperty("title").GetString());
        Assert.Equal(0, await ConDbAsync(async db => await db.Alumnos.CountAsync()));
    }

    // ─── Ayudas ────────────────────────────────────────────────────────────────

    private HttpClient Client() => _factory.CreateClient();

    private async Task<T> ConDbAsync<T>(Func<CapturasDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        return await read(db);
    }
}
