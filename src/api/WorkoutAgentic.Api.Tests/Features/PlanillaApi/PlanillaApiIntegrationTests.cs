using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

// Tests de integración del GET de la sesión hablante (Alumno · Planilla):
// WebApplicationFactory + BD efímera; el catálogo queda sembrado por el host.
// El aluno se busca por teléfono en el seed: el id no da por sentado nada.
public class PlanillaApiIntegrationTests : IDisposable
{
    // Teléfono del alumno del seed (cada test tiene su propia BD efímera).
    private const string TelefonoSeed = "5215512345678";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    // BD efímera por instancia (per test): el DbInitializer del host corre
    // Migrate() sobre un archivo exclusivo, sin pisar bases en paralelo.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-plan-{Guid.NewGuid():N}.db");

    public PlanillaApiIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // BD para tests: UseSetting tiene más derecho que la config de
            // appsettings en hosts minimal-API (el connection string se lee al
            // construir el host, antes de los callbacks de configuración).
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
    public async Task SesionActual_WithUnknownAlumno_Returns404Problem()
    {
        var response = await Client().GetAsync("/api/planillas/sesion-actual/4242");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Alumno inexistente", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SesionActual_WithAlumnoWithoutSessions_Returns200WithNullSession()
    {
        await SeedAsync(db => db.Alumnos.Add(new Alumno { TelefonoWa = TelefonoSeed }));
        var alumnoId = await AlumnoIdSeedAsync();

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(body.Sesion);
        Assert.Equal(alumnoId, body.Alumno.Id);
        Assert.Equal(TelefonoSeed, body.Alumno.TelefonoWa);
    }

    [Fact]
    public async Task SesionActual_WithOnlyConfirmedSessions_Returns200WithNullSession()
    {
        await SeedAsync(db =>
        {
            var sesion = new SesionCaptura
            {
                UltimaActividadUtc = DateTime.UtcNow,
                Estado = EstadoSesionCaptura.Confirmada,
            };
            db.Add(new Alumno { TelefonoWa = TelefonoSeed, Sesiones = [sesion] });
        });
        var alumnoId = await AlumnoIdSeedAsync();

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(body.Sesion);
    }

    [Fact]
    public async Task SesionActual_WithMixedSessions_ReturnsTheLatestNonConfirmed()
    {
        await SeedAsync(db =>
        {
            var confirmada = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.Confirmada,
                UltimaActividadUtc = DateTime.UtcNow.AddDays(-1),
            };
            var enCurso = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.EnCurso,
                UltimaActividadUtc = DateTime.UtcNow.AddDays(-1),
            };
            db.Add(new Alumno { TelefonoWa = TelefonoSeed, Sesiones = [confirmada, enCurso] });
        });
        var alumnoId = await AlumnoIdSeedAsync();
        var sesionEsperada = await ConDbAsync(async db =>
            await db.SesionesCaptura.AsNoTracking()
                .SingleAsync(s => s.Estado == EstadoSesionCaptura.EnCurso));

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.NotNull(body.Sesion);
        var sesion = body.Sesion!;
        Assert.Equal(sesionEsperada.Id, sesion.Id);
        Assert.Equal("EnCurso", sesion.Estado);
    }

    [Fact]
    public async Task SesionActual_WithEnCursoSession_ReturnsGridRowsWithRealNulls()
    {
        await SeedAsync(db =>
        {
            var prensa = new Ejercicio
            {
                NombreCanonico = "Prensa (prueba)",
                Apodos = ["prensa prueba"],
                GrupoMuscular = "Cuádriceps",
                UsaPeso = true,
            };
            var plancha = new Ejercicio
            {
                NombreCanonico = "Plancha (prueba)",
                Apodos = ["plancha prueba"],
                GrupoMuscular = "Core",
                UsaPeso = false,
                UsaTiempo = true,
            };
            var sesion = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.EnCurso,
                EjercicioActivo = prensa,
                OrdenSerieSiguiente = 2,
                UltimaActividadUtc = DateTime.UtcNow,
            };
            // Fila completa: serie 1 de prensa con peso.
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                Ejercicio = prensa,
                NroSerie = 1,
                Reps = 12,
                PesoKg = 45m,
                Unidad = Unidad.Kg,
                CreadoUtc = DateTime.UtcNow.AddMinutes(-5),
            });
            // Fila incompleta: serie 2 sin peso (aclaración abandonada).
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                Ejercicio = prensa,
                NroSerie = 2,
                Reps = 10,
                CreadoUtc = DateTime.UtcNow.AddMinutes(-4),
                PendienteAclaracion = true,
            });
            // Fila por tiempo: plancha con TiempoSeg, sin reps ni peso.
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                Ejercicio = plancha,
                NroSerie = 1,
                TiempoSeg = 60,
                Unidad = Unidad.Tiempo,
                CreadoUtc = DateTime.UtcNow.AddMinutes(-3),
            });
            db.Add(new Alumno { TelefonoWa = TelefonoSeed, Sesiones = [sesion] });
        });
        var alumnoId = await AlumnoIdSeedAsync();
        var (idPrensa, idPlancha) = await ConDbAsync(async db =>
        {
            var prensa = await db.Ejercicios.AsNoTracking().SingleAsync(e => e.NombreCanonico == "Prensa (prueba)");
            var plancha = await db.Ejercicios.AsNoTracking().SingleAsync(e => e.NombreCanonico == "Plancha (prueba)");
            return (prensa.Id, plancha.Id);
        });

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body.Sesion);
        var sesion = body.Sesion!;
        Assert.Equal("EnCurso", sesion.Estado);
        Assert.Equal(idPrensa, sesion.EjercicioActivoId);
        Assert.Equal("Prensa (prueba)", sesion.EjercicioActivoNombre);
        Assert.Equal(2, sesion.OrdenSerieSiguiente);
        Assert.Null(sesion.Aclaracion);

        // Filas en orden de creación (Id ascendente): la cuadrícula respeta
        // el orden en que el alumno cargó.
        var filas = sesion.Filas;
        Assert.Equal(3, filas.Count);
        Assert.True(filas[0].Id < filas[1].Id && filas[1].Id < filas[2].Id);

        // Serie completa: dato capturado, sin marcas.
        Assert.Equal(1, filas[0].NroSerie);
        Assert.Equal(idPrensa, filas[0].EjercicioId);
        Assert.Equal("Prensa (prueba)", filas[0].EjercicioCanonico);
        Assert.Equal(12, filas[0].Reps);
        Assert.Equal(45m, filas[0].PesoKg);
        Assert.Equal("Kg", filas[0].Unidad);
        Assert.Null(filas[0].TiempoSeg);
        Assert.False(filas[0].PendienteAclaracion);

        // Serie incompleta: el peso REAL es null (regla madre) + marca.
        Assert.Equal(10, filas[1].Reps);
        Assert.Null(filas[1].PesoKg);
        Assert.True(filas[1].PendienteAclaracion);

        // Serie por tiempo: TiempoSeg y unidad Tiempo, sin reps ni peso.
        Assert.Equal(idPlancha, filas[2].EjercicioId);
        Assert.Equal(60, filas[2].TiempoSeg);
        Assert.Equal("Tiempo", filas[2].Unidad);
        Assert.Null(filas[2].Reps);
        Assert.Null(filas[2].PesoKg);
    }

    [Fact]
    public async Task SesionActual_WithRowsWithoutExercise_ExposesCrudoAndDescriptionFlag()
    {
        await SeedAsync(db =>
        {
            var sesion = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.Expirada,
                UltimaActividadUtc = DateTime.UtcNow.AddHours(-3),
            };
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                EjercicioCrudo = "maquina que no conozco",
                DescripcionPendiente = true,
                NroSerie = 1,
                Reps = 8,
                CreadoUtc = DateTime.UtcNow.AddHours(-3),
                PendienteAclaracion = true,
            });
            db.Add(new Alumno { TelefonoWa = TelefonoSeed, Sesiones = [sesion] });
        });
        var alumnoId = await AlumnoIdSeedAsync();
        var filaId = await ConDbAsync(async db =>
            (await db.RegistrosParciales.AsNoTracking().SingleAsync()).Id);

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.NotNull(body.Sesion);
        var sesion = body.Sesion!;
        Assert.Equal("Expirada", sesion.Estado);
        var fila = Assert.Single(sesion.Filas);
        Assert.Equal(filaId, fila.Id);
        Assert.Null(fila.EjercicioId);
        Assert.Null(fila.EjercicioCanonico);
        // El crudo llega intacto: nada inventado, la SPA lo propone a editar.
        Assert.Equal("maquina que no conozco", fila.EjercicioCrudo);
        Assert.True(fila.DescripcionPendiente);
        Assert.True(fila.PendienteAclaracion);
    }

    [Fact]
    public async Task SesionActual_WithEsperandoAclaracion_ExposesQuestionOfChat()
    {
        await SeedAsync(db =>
        {
            var sesion = new SesionCaptura
            {
                Estado = EstadoSesionCaptura.EsperandoAclaracion,
                OrdenSerieSiguiente = 2,
                UltimaActividadUtc = DateTime.UtcNow,
            };
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                NroSerie = 1,
                Reps = 5,
                CreadoUtc = DateTime.UtcNow,
                PendienteAclaracion = true,
            });
            sesion.AclaracionPendiente = new AclaracionPendiente
            {
                Sesion = sesion,
                Campo = "peso",
                Reintento = 1,
                ExpiraUtc = DateTime.UtcNow.AddHours(2),
            };
            db.Add(new Alumno { TelefonoWa = TelefonoSeed, Sesiones = [sesion] });
        });
        var alumnoId = await AlumnoIdSeedAsync();

        var response = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.NotNull(body.Sesion);
        var sesion = body.Sesion!;
        Assert.Equal("EsperandoAclaracion", sesion.Estado);
        Assert.NotNull(sesion.Aclaracion);
        var aclaracion = sesion.Aclaracion!;
        Assert.Equal("peso", aclaracion.Campo);
        Assert.Equal(1, aclaracion.Reintento);
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

    private async Task<T> ConDbAsync<T>(Func<CapturasDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        return await read(db);
    }

    private async Task<int> AlumnoIdSeedAsync() => await ConDbAsync(async db =>
        (await db.Alumnos.AsNoTracking().SingleAsync(a => a.TelefonoWa == TelefonoSeed)).Id);

    private static RespuestaSesionActual Deserialize(string json) =>
        JsonSerializer.Deserialize<RespuestaSesionActual>(json, JsonOptions)
        ?? throw new InvalidOperationException("La respuesta no deserializa a RespuestaSesionActual.");
}
