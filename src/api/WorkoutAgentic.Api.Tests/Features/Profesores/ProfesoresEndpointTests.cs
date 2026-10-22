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
using WorkoutAgentic.Api.Features.Profesores;

namespace WorkoutAgentic.Api.Tests.Features.Profesores;

// Tests de la vista Profesor (ADR-0010): WebApplicationFactory + BD efímera.
// La lectura es del snapshot confirmado: jamás de sesiones ni parciales.
public class ProfesoresEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-profe-{Guid.NewGuid():N}.db");

    public ProfesoresEndpointTests()
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
    public async Task ListarAlumnos_DevuelveCuentasConResumenDePlanillas()
    {
        await SeedAsync(db =>
        {
            // Grafo completo: EF propaga AlumnoId/SesionId por las navegaciones.
            var sesion = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.Confirmada,
                UltimaActividadUtc = DateTime.UtcNow.AddHours(-2),
            };
            var planilla = new PlanillaConfirmada
            {
                Sesion = sesion,
                ConfirmadaUtc = DateTime.UtcNow.AddHours(-2),
            };
            planilla.Filas.Add(new PlanillaFila
            {
                Orden = 1,
                EjercicioCanonical = "Prensa de pierna",
                NroSerie = 1,
                Reps = 12,
                PesoKg = 45m,
                Unidad = Unidad.Kg,
            });
            db.Add(new Alumno { Nombre = "Lucía", TelefonoWa = "5491100112233" });
            db.Add(new Alumno
            {
                Nombre = "Bruno",
                Sesiones = [sesion],
                PlanillasConfirmadas = [planilla],
            });
        });

        var respuesta = await Client().GetAsync("/api/profesores/alumnos");
        var alumnos = await respuesta.Content
            .ReadFromJsonAsync<List<AlumnoProfesorVista>>(JsonOptions) ?? [];

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, alumnos.Count);

        // Orden por nombre; el resumen no inventa: Bruno tiene 1 planilla, Lucía 0.
        Assert.Equal("Bruno", alumnos[0].Nombre);
        Assert.Equal("Lucía", alumnos[1].Nombre);
        var bruno = alumnos[0];
        Assert.Equal(1, bruno.PlanillasConfirmadas);
        Assert.NotNull(bruno.UltimaConfirmadaUtc);
        Assert.Null(bruno.TelefonoWa); // alumno del pivote: sin WhatsApp ya es válido
        var lucia = alumnos[1];
        Assert.Equal(0, lucia.PlanillasConfirmadas);
        Assert.Null(lucia.UltimaConfirmadaUtc);
    }

    [Fact]
    public async Task PlanillasDelAlumno_WithUnknownAlumno_Returns404Problem()
    {
        var respuesta = await Client().GetAsync("/api/profesores/alumnos/4242/planillas");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problem = await respuesta.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Alumno inexistente", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PlanillasDelAlumno_WithoutPlanillas_Returns200WithEmptyList()
    {
        var alumnoId = await SeedAlumnoConPlanillasAsync([]);

        var respuesta = await Client().GetAsync($"/api/profesores/alumnos/{alumnoId}/planillas");
        var planillas = await respuesta.Content
            .ReadFromJsonAsync<List<PlanillaConfirmadaVista>>(JsonOptions) ?? [];

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(planillas);
    }

    [Fact]
    public async Task PlanillasDelAlumno_DevuelveLasConfirmadasEnOrdenDescendente()
    {
        var (primera, segunda) = await SeedAlumnoConDosPlanillasAsync();
        var alumnoId = await ConDbAsync(async db =>
            (await db.Alumnos.AsNoTracking().SingleAsync(a => a.Nombre == "Historia")).Id);

        var respuesta = await Client().GetAsync($"/api/profesores/alumnos/{alumnoId}/planillas");
        var planillas = await respuesta.Content
            .ReadFromJsonAsync<List<PlanillaConfirmadaVista>>(JsonOptions) ?? [];

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, planillas.Count);
        // La más nueva primero: la vista del profesor es un historial.
        Assert.Equal(segunda, planillas[0].Id);
        Assert.Equal(primera, planillas[1].Id);
        Assert.Contains(planillas[0].Filas, f => f.EjercicioCanonical == "Prensa (prueba)" && f.PesoKg == 45m);
        Assert.Contains(planillas[0].Filas, f => f.Incompleta);
        // El snapshot no viaja sin su sesión de origen (contrato completo).
        Assert.True(planillas[0].SesionId > 0);
    }

    // ─── Ayudas ────────────────────────────────────────────────────────────────

    private HttpClient Client() => _factory.CreateClient();

    private async Task SeedAsync(Action<CapturasDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedAlumnoConPlanillasAsync(PlanillaConfirmada[] planillas)
    {
        await SeedAsync(db => db.Alumnos.Add(
            new Alumno { Nombre = "Test Alumno", PlanillasConfirmadas = [.. planillas] }));
        return await ConDbAsync(async db =>
            (await db.Alumnos.AsNoTracking().SingleAsync(a => a.Nombre == "Test Alumno")).Id);
    }

    private async Task<(int Primera, int Segunda)> SeedAlumnoConDosPlanillasAsync()
    {
        await SeedAsync(db =>
        {
            // Grafo completo por planilla: cada confirmada nace de su propia
            // sesión (EF propaga AlumnoId/SesionId por las navegaciones).
            var primeraPlanilla = new PlanillaConfirmada
            {
                Sesion = new SesionCaptura
                {
                    Estado = EstadoSesionCaptura.Confirmada,
                    UltimaActividadUtc = DateTime.UtcNow.AddDays(-1),
                },
                ConfirmadaUtc = DateTime.UtcNow.AddDays(-1),
            };
            primeraPlanilla.Filas.Add(new PlanillaFila
            {
                Orden = 1,
                EjercicioCanonical = "Prensa (prueba)",
                NroSerie = 1,
                Reps = 10,
                PesoKg = 40m,
                Unidad = Unidad.Kg,
            });
            var segundaPlanilla = new PlanillaConfirmada
            {
                Sesion = new SesionCaptura
                {
                    Estado = EstadoSesionCaptura.Confirmada,
                    UltimaActividadUtc = DateTime.UtcNow.AddHours(-3),
                },
                ConfirmadaUtc = DateTime.UtcNow.AddHours(-3),
            };
            segundaPlanilla.Filas.Add(new PlanillaFila
            {
                Orden = 1,
                EjercicioCanonical = "Prensa (prueba)",
                NroSerie = 1,
                Reps = 12,
                PesoKg = 45m,
                Unidad = Unidad.Kg,
            });
            segundaPlanilla.Filas.Add(new PlanillaFila
            {
                Orden = 2,
                EjercicioCanonical = "hice quedadas",
                NroSerie = 2,
                Incompleta = true,
            });
            // El id real se lee después: el autogenerado no existe aún.
            db.Add(new Alumno
            {
                Nombre = "Historia",
                Sesiones =
                [
                    primeraPlanilla.Sesion!,
                    segundaPlanilla.Sesion!,
                ],
                PlanillasConfirmadas = [primeraPlanilla, segundaPlanilla],
            });
        });

        return await ConDbAsync(async db =>
        {
            var ordenadas = await db.PlanillasConfirmadas.AsNoTracking()
                .OrderBy(p => p.ConfirmadaUtc)
                .ToListAsync();
            return (ordenadas.First().Id, ordenadas.Last().Id);
        });
    }

    private async Task<T> ConDbAsync<T>(Func<CapturasDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        return await read(db);
    }
}
