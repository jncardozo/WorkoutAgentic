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

// Tests de integración del CRUD de parciales y de la confirmación (reglas
// de negocio §2): WebApplicationFactory + BD efímera por instancia. El
// catálogo queda sembrado por el host (Plancha = UsaTiempo).
public class PlanillaApiMutacionesTests : IDisposable
{
    private const string NombrePrensa = "Prensa (prueba)";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    // BD efímera por instancia (per test): el DbInitializer del host corre
    // Migrate() sobre un archivo exclusivo, sin pisar bases en paralelo.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-mut-{Guid.NewGuid():N}.db");

    public PlanillaApiMutacionesTests()
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

    // ─── Agregar fila (POST /filas) ─────────────────────────────────────────────

    [Fact]
    public async Task AgregarFila_WithUnknownAlumno_Returns404Problem()
    {
        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(999, 1, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Alumno inexistente", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AgregarFila_WithNoSession_Returns409Problem()
    {
        await SeedAsync(db => db.Alumnos.Add(new Alumno { Nombre = "Alumno de prueba" }));
        var alumnoId = await IdDelAlumnoAsync();

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, 1, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Sin sesión activa de captura", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AgregarFila_WithChatWaitingClarification_Returns409AndAddsNothing()
    {
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.EsperandoAclaracion, conFilaPendiente: true, conAclaracion: true);

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, 1, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Aclaración en curso", problem.GetProperty("title").GetString());
        // Nada se agregó: la cuadrícula queda congelada mientras el bot espera.
        Assert.Equal(1, await ContarFilasDeHablanteAsync(alumnoId));
    }

    [Fact]
    public async Task AgregarFila_WithExpiredSession_Returns409Problem()
    {
        var (alumnoId, _) = await SeedBaseAsync(EstadoSesionCaptura.Expirada);

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, 1, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Sesión pausada por inactividad", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AgregarFila_WithUnknownEjercicio_Returns400Problem()
    {
        var (alumnoId, _) = await SeedBaseAsync(EstadoSesionCaptura.EnCurso);

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, 4242, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Ejercicio inexistente en el catálogo", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task AgregarFila_IncompleteRow_Returns400AndAddsNothing()
    {
        var (alumnoId, prensaId) = await SeedBaseAsync(
            EstadoSesionCaptura.EnCurso, conFilaCompleta: false, conPrensa: true);

        // Prensa (UsaPeso) sin peso: fila incompleta — el API no inventa.
        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, prensaId, 1, Reps: 10, PesoKg: null, Unidad: null, TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Datos de serie inválidos", problem.GetProperty("title").GetString());
        Assert.Contains("necesita el peso", problem.GetProperty("detail").GetString());
        Assert.Equal(0, await ContarFilasDeHablanteAsync(alumnoId));
    }

    [Fact]
    public async Task AgregarFila_WithNegativeReps_Returns400()
    {
        var (alumnoId, prensaId) = await SeedBaseAsync(
            EstadoSesionCaptura.EnCurso, conFilaCompleta: false, conPrensa: true);

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, prensaId, 1, Reps: -3, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Las repeticiones van de 1 en adelante", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AgregarFila_WithLbUnit_NormalizesToKg()
    {
        var (alumnoId, prensaId) = await SeedBaseAsync(
            EstadoSesionCaptura.EnCurso, conFilaCompleta: false, conPrensa: true);

        // "100 lb" (ADR-0004 S6): la planilla queda 100 % en kg.
        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, prensaId, 1, Reps: 10, PesoKg: 100m, Unidad: "Lb", TiempoSeg: null));
        var body = await DeserializarSesionAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fila = Assert.Single(body.Sesion!.Filas);
        Assert.Equal(45.36m, fila.PesoKg);
        Assert.Equal("Kg", fila.Unidad);
        Assert.Equal(10, fila.Reps);
    }

    [Fact]
    public async Task AgregarFila_ByTiempoExercise_StoresTiempoSeg()
    {
        var (alumnoId, planchaId) = await SeedPlanchaAsync(EstadoSesionCaptura.EnCurso);

        var response = await PostAsync("/api/planillas/filas",
            new ComandoNuevaFila(alumnoId, planchaId, 1, Reps: null, PesoKg: null, Unidad: null, TiempoSeg: 60));
        var body = await DeserializarSesionAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fila = Assert.Single(body.Sesion!.Filas);
        Assert.Equal(60, fila.TiempoSeg);
        Assert.Equal("Tiempo", fila.Unidad);
        Assert.Null(fila.Reps);
        Assert.Null(fila.PesoKg);
    }

    // ─── Editar fila (PUT /filas/{id}) ──────────────────────────────────────────

    [Fact]
    public async Task EditarFila_WithUnknownFila_Returns404Problem()
    {
        var response = await PutAsync("/api/planillas/filas/4242",
            new ComandoEdicionFila(1, null, 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditarFila_WhileChatWaitingClarification_Returns409Problem()
    {
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.EsperandoAclaracion, conFilaCompleta: true, conAclaracion: true);
        var filaId = await IdUnicaFilaDeHablanteAsync(alumnoId);

        var response = await PutAsync($"/api/planillas/filas/{filaId}",
            new ComandoEdicionFila(null, "cambio por la unclear", 1, Reps: 10, PesoKg: 40, Unidad: "Kg", TiempoSeg: null));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Aclaración en curso", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task EditarFila_CompletesPendingRow_ClearsFlag()
    {
        var (alumnoId, prensaId) = await SeedBaseAsync(
            EstadoSesionCaptura.EnCurso, conFilaPendiente: true, conPrensa: true);
        var filaId = await IdUnicaFilaDeHablanteAsync(alumnoId);

        // El alumno completa el peso que el chat abandonó: la marca se limpia.
        var response = await PutAsync($"/api/planillas/filas/{filaId}",
            new ComandoEdicionFila(prensaId, null, 1, Reps: 10, PesoKg: 45, Unidad: "Kg", TiempoSeg: null));
        var body = await DeserializarSesionAsync(response);

        var fila = Assert.Single(body.Sesion!.Filas);
        Assert.Equal(45m, fila.PesoKg);
        Assert.Equal("Kg", fila.Unidad);
        Assert.False(fila.PendienteAclaracion);
    }

    [Fact]
    public async Task EditarFila_WithoutExercise_KeepsStudentDescription()
    {
        var (alumnoId, prensaId) = await SeedBaseAsync(
            EstadoSesionCaptura.Lista, conFilaCompleta: true, conPrensa: true);
        var filaId = await IdUnicaFilaDeHablanteAsync(alumnoId);

        // La fila vuelve a descripción libre (ADR-0004 S7, "otro"): el crudo
        // escrito por el alumno es la identidad de la fila.
        var response = await PutAsync($"/api/planillas/filas/{filaId}",
            new ComandoEdicionFila(null, "maquina desconocida", 1, Reps: 8, PesoKg: null, Unidad: null, TiempoSeg: null));
        var body = await DeserializarSesionAsync(response);

        var fila = Assert.Single(body.Sesion!.Filas);
        Assert.Null(fila.EjercicioId);
        Assert.Equal("maquina desconocida", fila.EjercicioCrudo);
        Assert.True(fila.DescripcionPendiente);
        Assert.Equal(8, fila.Reps);
    }

    [Fact]
    public async Task EditarFila_WithTiempoExerciseAndPeso_Returns400()
    {
        var (alumnoId, planchaId) = await SeedPlanchaAsync(EstadoSesionCaptura.EnCurso, tiempoSeg: 45);
        var filaId = await IdUnicaFilaDeHablanteAsync(alumnoId);

        var response = await PutAsync($"/api/planillas/filas/{filaId}",
            new ComandoEdicionFila(planchaId, null, 1, Reps: null, PesoKg: 30, Unidad: "Kg", TiempoSeg: 45));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Datos de serie inválidos", problem.GetProperty("title").GetString());
        Assert.Contains("no lleva peso", problem.GetProperty("detail").GetString());
    }

    // ─── Eliminar fila (DELETE /filas/{id}) ─────────────────────────────────────

    [Fact]
    public async Task EliminarFila_RemovesRowFromGrid()
    {
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.Lista, conFilaCompleta: true, conPrensa: true);
        var filaId = await IdUnicaFilaDeHablanteAsync(alumnoId);

        var response = await Client().DeleteAsync($"/api/planillas/filas/{filaId}");
        var body = await DeserializarSesionAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.Sesion!.Filas);
    }

    // ─── Confirmar (POST /confirmar) ────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_WithUnknownAlumno_Returns404Problem()
    {
        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(4242));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Alumno inexistente", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Confirmar_WithEnCursoSession_Returns409Problem()
    {
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.EnCurso, conFilaCompleta: true, conPrensa: true);

        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("La sesión no está lista para confirmar", problem.GetProperty("title").GetString());
        Assert.Contains("EnCurso", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Confirmar_WithEmptyLista_Returns400Problem()
    {
        var (alumnoId, _) = await SeedBaseAsync(EstadoSesionCaptura.Lista);

        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("La planilla está vacía", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Confirmar_WithLista_CreatesFrozenSnapshot_AndLocksSession()
    {
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.Lista, conFilaCompleta: true, conFilaPendiente: true, conPrensa: true);

        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        var snapshot = await response.Content.ReadFromJsonAsync<PlanillaConfirmadaVista>(JsonOptions);

        // Snapshot: fila completa → intacta; fila pendiente (sin peso) → marcada.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, snapshot!.Filas.Count);

        var completa = snapshot.Filas[0];
        Assert.Equal(1, completa.Orden);
        Assert.Equal(NombrePrensa, completa.EjercicioCanonical);
        Assert.Equal(12, completa.Reps);
        Assert.Equal(45m, completa.PesoKg);
        Assert.Equal("Kg", completa.Unidad);
        Assert.False(completa.Incompleta);

        var pendiente = snapshot.Filas[1];
        Assert.Equal(2, pendiente.Orden);
        Assert.Equal(10, pendiente.Reps);
        Assert.Null(pendiente.PesoKg);
        Assert.True(pendiente.Incompleta);

        // La sesión queda Confirmada: ya no se expone como hablante (el
        // próximo mensaje del chat abrirá sesión nueva).
        var getResponse = await Client().GetAsync($"/api/planillas/sesion-actual/{alumnoId}");
        var getBody = await DeserializarSesionAsync(getResponse);
        Assert.Null(getBody.Sesion);

        // Segundo confirm: no hay sesión en Lista otra vez.
        var second = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Confirmar_WithTiempoRow_FreezesTiempoSegAndCanonical()
    {
        var (alumnoId, _) = await SeedPlanchaAsync(EstadoSesionCaptura.Lista, tiempoSeg: 60);

        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        var snapshot = await response.Content.ReadFromJsonAsync<PlanillaConfirmadaVista>(JsonOptions);

        var fila = Assert.Single(snapshot!.Filas);
        Assert.Equal("Plancha", fila.EjercicioCanonical);
        Assert.Equal(60, fila.TiempoSeg);
        Assert.Equal("Tiempo", fila.Unidad);
        Assert.Null(fila.Reps);
        Assert.False(fila.Incompleta);
    }

    [Fact]
    public async Task Confirmar_WithCrudDescriptionRow_FreezesCrudoAsCanonical()
    {
        // Fila "otro" sin ejercicio (descripción libre): el snapshot congela
        // el texto del alumno como etiqueta y marca la fila incompleta.
        var (alumnoId, _) = await SeedBaseAsync(
            EstadoSesionCaptura.Lista, conFilaSinEjercicio: true);

        var response = await PostAsync("/api/planillas/confirmar", new ComandoConfirmar(alumnoId));
        var snapshot = await response.Content.ReadFromJsonAsync<PlanillaConfirmadaVista>(JsonOptions);

        var fila = Assert.Single(snapshot!.Filas);
        Assert.Equal("maquina que no conozco", fila.EjercicioCanonical);
        Assert.True(fila.Incompleta);
    }

    // ─── Ayudas ────────────────────────────────────────────────────────────────

    private HttpClient Client() => _factory.CreateClient();

    private Task<HttpResponseMessage> PostAsync<TBody>(string url, TBody body) =>
        Client().PostAsJsonAsync(url, body, JsonOptions);

    private Task<HttpResponseMessage> PutAsync<TBody>(string url, TBody body) =>
        Client().PutAsJsonAsync(url, body, JsonOptions);

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

    private async Task<int> IdDelAlumnoAsync() => await ConDbAsync(async db =>
        (await db.Alumnos.AsNoTracking().SingleAsync(a => a.Nombre == "Alumno de prueba")).Id);

    private async Task<RespuestaSesionActual> DeserializarSesionAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<RespuestaSesionActual>(
            await response.Content.ReadAsStringAsync(), JsonOptions)
        ?? throw new InvalidOperationException("La respuesta no deserializa a RespuestaSesionActual.");

    // Semilla base: alumno + sesión hablante en el estado pedido, con prensa
    // (UsaPeso) y combinaciones de fila según flags. Devuelve alumno y prensa.
    private async Task<(int AlumnoId, int IdPrensa)> SeedBaseAsync(
        EstadoSesionCaptura estado,
        bool conFilaCompleta = false,
        bool conFilaPendiente = false,
        bool conFilaSinEjercicio = false,
        bool conAclaracion = false,
        bool conPrensa = false)
    {
        await SeedAsync(db =>
        {
            var prensa = new Ejercicio
            {
                NombreCanonico = NombrePrensa,
                Apodos = ["prensa prueba"],
                GrupoMuscular = "Cuádriceps",
                UsaPeso = true,
            };
            var ahora = DateTime.UtcNow;
            var sesion = new SesionCaptura
            {
                Estado = estado,
                EjercicioActivo = conPrensa ? prensa : null,
                UltimaActividadUtc = ahora,
            };

            if (conFilaCompleta)
            {
                sesion.Registros.Add(new RegistroParcial
                {
                    Sesion = sesion,
                    Ejercicio = prensa,
                    NroSerie = 1,
                    Reps = 12,
                    PesoKg = 45m,
                    Unidad = Unidad.Kg,
                    CreadoUtc = ahora,
                });
            }

            if (conFilaPendiente)
            {
                sesion.Registros.Add(new RegistroParcial
                {
                    Sesion = sesion,
                    Ejercicio = prensa,
                    NroSerie = 2,
                    Reps = 10,
                    CreadoUtc = ahora,
                    PendienteAclaracion = true,
                });
            }

            if (conFilaSinEjercicio)
            {
                sesion.Registros.Add(new RegistroParcial
                {
                    Sesion = sesion,
                    EjercicioCrudo = "maquina que no conozco",
                    DescripcionPendiente = true,
                    NroSerie = 1,
                    Reps = 8,
                    CreadoUtc = ahora,
                    PendienteAclaracion = true,
                });
            }

            if (conAclaracion)
            {
                sesion.AclaracionPendiente = new AclaracionPendiente
                {
                    Sesion = sesion,
                    Campo = "peso",
                    Reintento = 1,
                    ExpiraUtc = ahora.AddHours(2),
                };
            }

            db.Add(new Alumno { Nombre = "Alumno de prueba", Sesiones = [sesion] });
        });

        // La prensa solo existe en la BD si alguna fila la referenció (nav) o
        // si se pidió explícitamente; los tests de sesión vacía no la consultan.
        var prensaId = await ConDbAsync(async db => (await db.Ejercicios
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.NombreCanonico == NombrePrensa))?.Id ?? 0);
        return (await IdDelAlumnoAsync(), prensaId);
    }

    // Semilla con la Plancha del catálogo sembrado (UsaTiempo): sesión con o
    // sin fila por tiempo según se pida.
    private async Task<(int AlumnoId, int IdPlancha)> SeedPlanchaAsync(
        EstadoSesionCaptura estado, int? tiempoSeg = null)
    {
        await SeedAsync(db =>
        {
            var plancha = db.Ejercicios.First(e => e.NombreCanonico == "Plancha");
            var ahora = DateTime.UtcNow;
            var sesion = new SesionCaptura
            {
                Estado = estado,
                EjercicioActivo = plancha,
                UltimaActividadUtc = ahora,
            };

            if (tiempoSeg is int segundos)
            {
                sesion.Registros.Add(new RegistroParcial
                {
                    Sesion = sesion,
                    Ejercicio = plancha,
                    NroSerie = 1,
                    TiempoSeg = segundos,
                    Unidad = Unidad.Tiempo,
                    CreadoUtc = ahora,
                });
            }

            db.Add(new Alumno { Nombre = "Alumno de prueba", Sesiones = [sesion] });
        });

        var planchaId = await ConDbAsync(async db =>
            (await db.Ejercicios.AsNoTracking()
                .Where(e => e.NombreCanonico == "Plancha")
                .Select(e => e.Id)
                .SingleAsync()));
        return (await IdDelAlumnoAsync(), planchaId);
    }

    private async Task<int> IdUnicaFilaDeHablanteAsync(int alumnoId) => await ConDbAsync(async db =>
        (await db.RegistrosParciales.AsNoTracking()
            .SingleAsync(r => r.Sesion!.AlumnoId == alumnoId
                && r.Sesion.Estado != EstadoSesionCaptura.Confirmada))
        .Id);

    private async Task<int> ContarFilasDeHablanteAsync(int alumnoId) => await ConDbAsync(async db =>
        await db.RegistrosParciales.AsNoTracking()
            .Where(r => r.Sesion!.AlumnoId == alumnoId
                && r.Sesion.Estado != EstadoSesionCaptura.Confirmada)
            .CountAsync());
}
