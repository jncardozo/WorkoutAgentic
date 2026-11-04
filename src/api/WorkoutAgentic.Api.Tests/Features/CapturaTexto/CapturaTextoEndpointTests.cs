using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.CapturaSesion;
using WorkoutAgentic.Api.Features.CapturaTexto;
using WorkoutAgentic.Api.Features.Nlu;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.CapturaTexto;

// Tests de integración del POST /api/capturas/{id}/texto (ADR-0009):
// WebApplicationFactory + BD efímera. El NLU es SINTÉTICO (guion texto →
// análisis): no se llama a proveedores externos; el resto del pipeline
// (servicio de sesión, catálogo, vistas) corre real.
public class CapturaTextoEndpointTests : IDisposable
{

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly NluDePrueba _nlu = new();

    // BD efímera por instancia (per test): el DbInitializer del host corre
    // Migrate() sobre un archivo exclusivo, igual que en PlanillaApi tests.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-cap-{Guid.NewGuid():N}.db");

    public CapturaTextoEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMensajesNlu>();
                services.AddSingleton<IMensajesNlu>(_nlu);
            });
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
    public async Task CapturarTexto_WithEmptyText_Returns400AndDoesNotConsultNlu()
    {
        var response = await Client().PostAsJsonAsync("/api/capturas/7/texto", new { texto = "   " }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Texto requerido", problem.GetProperty("title").GetString());
        Assert.Equal(0, _nlu.Consultas);
        Assert.Equal(0, await SesionesEnDbAsync());
    }

    [Fact]
    public async Task CapturarTexto_WithUnknownAlumno_Returns404WithoutCreatingSession()
    {
        // Guion cargado a propósito: si el borde consultara el NLU, el test
        // fallaría (Consultas > 0).
        _nlu.Guion("prensa 8 con 45", AnalisisAlta(1, "Prensa de pierna", "prensa 8 con 45"));

        var response = await Client().PostAsJsonAsync("/api/capturas/4242/texto", new { texto = "prensa 8 con 45" }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Alumno inexistente", problem.GetProperty("title").GetString());
        // No se consulta el NLU ni se deja estado: el borde valida primero.
        Assert.Equal(0, _nlu.Consultas);
        Assert.Equal(0, await SesionesEnDbAsync());
    }

    [Fact]
    public async Task CapturarTexto_WithLoteBase_CreaFilasYDejaDatosDeSerieVacios()
    {
        var (alumnoId, ejercicioId, canonico) = await SeedAlumnoConEjercicioAsync("Prensa (prueba)");
        _nlu.Guion("prensa 4 series", AnalisisAlta(ejercicioId, canonico, texto: "prensa 4 series", cantidadSeries: 4));

        var response = await Client().PostAsJsonAsync($"/api/capturas/{alumnoId}/texto", new { texto = "prensa 4 series" }, JsonOptions);
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TextosBot.SeriesAnotadasBase(canonico, 1, 4), body.MensajeAlumno);
        var sesion = body.Respuesta.Sesion!;
        Assert.Equal("EnCurso", sesion.Estado);
        Assert.Equal(canonico, sesion.EjercicioActivoNombre);
        // Lote base (ADR-0013): 4 filas con solo ejercicio y serie; los datos
        // de serie quedan null para completarse después.
        Assert.Equal(4, sesion.Filas!.Count);
        Assert.All(sesion.Filas, fila =>
        {
            Assert.Null(fila.Reps);
            Assert.Null(fila.PesoKg);
            Assert.Equal(canonico, fila.EjercicioCanonico);
        });
        Assert.Equal(5, sesion.OrdenSerieSiguiente);

        // La sesión creada es la misma que devuelve la vista de planilla.
        var sesionIdDb = await ConDbAsync(async db =>
            (await db.SesionesCaptura.AsNoTracking().SingleAsync(s => s.AlumnoId == alumnoId)).Id);
        Assert.Equal(sesionIdDb, sesion.Id);
    }

    [Fact]
    public async Task CapturarTexto_WithSeriesSinEjercicio_EnseniaLaReglaYNoTocaEstado()
    {
        var alumnoId = await SeedAlumnoAsync();
        _nlu.Guion("3 series de 12", AnalisisSeriesSinEjercicio("3 series de 12"));

        var response = await Client().PostAsJsonAsync($"/api/capturas/{alumnoId}/texto", new { texto = "3 series de 12" }, JsonOptions);
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TextosBot.FueraDeRegla, body.MensajeAlumno);
        var sesion = body.Respuesta.Sesion!;
        Assert.Equal("EnCurso", sesion.Estado);
        Assert.Null(sesion.Aclaracion);
        // Fuera de la regla: nadie cambió el estado ni se apoyó en el activo.
        Assert.Null(sesion.EjercicioActivoNombre);
        Assert.Empty(sesion.Filas);
        Assert.Equal(1, sesion.OrdenSerieSiguiente);
    }

    [Fact]
    public async Task CapturarTexto_WhenAnalysisFails_Returns503WithoutTouchingState()
    {
        var alumnoId = await SeedAlumnoAsync();
        _nlu.Guion("garabato", null); // null = etapa de análisis falló

        var response = await Client().PostAsJsonAsync($"/api/capturas/{alumnoId}/texto", new { texto = "garabato" }, JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("No pudimos interpretar la captura", problem.GetProperty("title").GetString());
        // Nada se aplicó: ni sesión ni filas (regla madre de etapas separadas).
        Assert.Equal(0, await SesionesEnDbAsync());
    }

    [Fact]
    public async Task CapturarTexto_WithClasificacionFallida_RespondsSinTocarEstado()
    {
        var alumnoId = await SeedAlumnoAsync();
        _nlu.Guion("no entiendo nada", new AnalisisCaptura(
            "no entiendo nada",
            ClasificacionIntent.Fallo("No salió la clasificación"),
            Entidades: null,
            ResolucionEjercicio: null));

        var response = await Client().PostAsJsonAsync($"/api/capturas/{alumnoId}/texto", new { texto = "no entiendo nada" }, JsonOptions);
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TextosBot.FalloLectura, body.MensajeAlumno);
        var sesion = body.Respuesta.Sesion!;
        Assert.Equal("EnCurso", sesion.Estado);
        Assert.Empty(sesion.Filas);
    }

    [Fact]
    public async Task CapturarTexto_WithSeguirOnExpirada_ReanudaLaSesion()
    {
        var alumnoId = await SeedAlumnoConSesionExpiradaAsync();
        // "seguir" pasa por ParseadorAclaraciones mirando el texto crudo:
        // el intent queda inválido (no aplica) igual que en los tests de servicio.
        _nlu.Guion("seguir", AnalisisTexto("seguir", IntentoCaptura.Invalido));

        var response = await Client().PostAsJsonAsync($"/api/capturas/{alumnoId}/texto", new { texto = "seguir" }, JsonOptions);
        var body = Deserialize(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TextosBot.Seguimos, body.MensajeAlumno);
        var sesion = body.Respuesta.Sesion!;
        Assert.Equal("EnCurso", sesion.Estado);
        Assert.Null(sesion.Aclaracion);
    }

    // ─── Ayudas ────────────────────────────────────────────────────────────────

    private HttpClient Client() => _factory.CreateClient();

    private async Task<T> ConDbAsync<T>(Func<CapturasDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        return await read(db);
    }

    private async Task<int> SesionesEnDbAsync() => await ConDbAsync(async db =>
        await db.SesionesCaptura.CountAsync());

    private async Task<int> SeedAlumnoAsync() => await ConDbAsync(async db =>
    {
        var alumno = new Alumno { Nombre = "Alumno de prueba" };
        db.Alumnos.Add(alumno);
        await db.SaveChangesAsync();
        return alumno.Id;
    });

    private async Task<(int AlumnoId, int EjercicioId, string Canonico)> SeedAlumnoConEjercicioAsync(
        string canonico) => await ConDbAsync(async db =>
    {
        var alumno = new Alumno { Nombre = "Alumno de prueba" };
        var ejercicio = new Ejercicio
        {
            NombreCanonico = canonico,
            Apodos = ["prensa"],
            GrupoMuscular = "pierna",
            UsaPeso = true,
        };
        db.Alumnos.Add(alumno);
        db.Ejercicios.Add(ejercicio);
        await db.SaveChangesAsync();
        return (alumno.Id, ejercicio.Id, canonico);
    });

    private async Task<int> SeedAlumnoConSesionExpiradaAsync() => await ConDbAsync(async db =>
    {
        var sesion = new SesionCaptura
        {
            Estado = EstadoSesionCaptura.Expirada,
            UltimaActividadUtc = DateTime.UtcNow.AddHours(-3),
        };
        db.Add(new Alumno { Nombre = "Alumno de prueba", Sesiones = [sesion] });
        await db.SaveChangesAsync();
        return sesion.AlumnoId;
    });

    private static RespuestaCapturaTextoVista Deserialize(string json) =>
        JsonSerializer.Deserialize<RespuestaCapturaTextoVista>(json, JsonOptions)
        ?? throw new InvalidOperationException("La respuesta no deserializa a RespuestaCapturaTextoVista.");

    // ─── Análisis sintéticos (mismas formas que SesionCapturaServicioTests) ────

    private static ClasificacionIntent Clasif(IntentoCaptura intento) => ClasificacionIntent.Ok(intento, 0.9);

    private static AnalisisCaptura AnalisisTexto(string texto, IntentoCaptura intento) =>
        new(texto, Clasif(intento), null, null);

    private static AnalisisCaptura AnalisisAlta(
        int ejercicioId, string canonico, string texto, int? cantidadSeries = null) =>
        new(
            texto,
            Clasif(IntentoCaptura.CargaEjercicio),
            new EntidadesNlu(
                EjercicioTexto: canonico, SerieOrdinal: null, CantidadSeries: cantidadSeries,
                Reps: null, PesoKg: null, Unidad: null, TiempoSegundos: null,
                DescansoSegundos: null, CampoAclarar: null, PreguntaAclarar: null),
            ResolucionEjercicio: ResultadoResolucionEjercicio.Matcheado(ejercicioId, canonico, 1.0));

    private static AnalisisCaptura AnalisisSeriesSinEjercicio(string texto) =>
        // Dictado fuera de la regla (ADR-0013): series SIN ejercicio nombrado.
        new(
            texto,
            Clasif(IntentoCaptura.CargaSerie),
            new EntidadesNlu(
                EjercicioTexto: null, SerieOrdinal: null, CantidadSeries: 3,
                Reps: 12, PesoKg: 60m, Unidad: Unidad.Kg, TiempoSegundos: null,
                DescansoSegundos: null, CampoAclarar: null, PreguntaAclarar: null),
            ResolucionEjercicio: null);

    // NLU sintético con guion texto → análisis (null = etapa fallida). Los
    // tests NO reimplementan NLU: las etapas reales tienen su propia suite.
    private sealed class NluDePrueba : IMensajesNlu
    {
        private readonly Dictionary<string, AnalisisCaptura?> _guion = new(StringComparer.Ordinal);

        public int Consultas { get; private set; }

        public void Guion(string texto, AnalisisCaptura? analisis) => _guion[texto] = analisis;

        public Task<AnalisisCaptura?> AnalizarTextoAsync(
            string texto, CancellationToken cancellationToken)
        {
            Consultas++;
            if (!_guion.TryGetValue(texto, out var analisis))
            {
                throw new InvalidOperationException($"El test no guionó el texto: '{texto}'.");
            }

            return Task.FromResult(analisis);
        }
    }
}
