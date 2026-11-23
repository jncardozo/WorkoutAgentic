using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.PlanillaApi;

namespace WorkoutAgentic.Api.Tests.Features.PlanillaApi;

public sealed class TrainingHistoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wa-history-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;

    public TrainingHistoryTests()
    {
        Directory.CreateDirectory(_directory);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={Path.Combine(_directory, "history.db")}");
            builder.UseSetting("Auth:DataProtectionKeysPath", Path.Combine(_directory, "keys"));
            builder.UseSetting("EmailDelivery:Provider", "invalid-test-provider");
        });
    }

    private async Task<(HttpClient Client, int StudentId)> Login(string email = "history@example.test", bool accept = true)
    {
        var client = AuthTestClient.Anonymous(_factory);
        await AuthTestClient.RefreshCsrf(client);
        var register = await client.PostAsJsonAsync("/api/alumnos", new RegisterRequest("QA", email, AuthTestClient.Password));
        register.EnsureSuccessStatusCode();
        var account = (await register.Content.ReadFromJsonAsync<AccountView>())!;
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, AuthTestClient.Password))).EnsureSuccessStatusCode();
        await AuthTestClient.RefreshCsrf(client);
        if (accept) await AuthTestClient.AcceptAgreement(client);
        return (client, account.StudentId);
    }

    private async Task<PlanillaConfirmada> Seed(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        var sheet = new PlanillaConfirmada
        {
            AlumnoId = studentId, ConfirmadaUtc = DateTime.UtcNow,
            Sesion = new SesionCaptura { AlumnoId = studentId, Estado = EstadoSesionCaptura.Confirmada, UltimaActividadUtc = DateTime.UtcNow },
            Filas = [new PlanillaFila { Orden = 1, EjercicioCanonical = "Sentadilla", NroSerie = 1, Reps = 10, PesoKg = 40, Unidad = Unidad.Kg }],
        };
        db.PlanillasConfirmadas.Add(sheet);
        await db.SaveChangesAsync();
        return sheet;
    }

    [Fact]
    public async Task Migration_ExistingDatabase_PreservesConfirmedRowsWithEmptyMetadata()
    {
        var options = new DbContextOptionsBuilder<CapturasDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "upgrade.db")}").Options;
        await using var db = new CapturasDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261119181235_AddAgreementAcceptances");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Alumno (Id, Nombre, Estado) VALUES (101, 'QA migración', 'ACTIVO');
            INSERT INTO SesionCaptura (Id, AlumnoId, Estado, OrdenSerieSiguiente, UltimaActividadUtc, RowVersion)
                VALUES (101, 101, 'Confirmada', 2, '2026-11-19 15:00:00', 1);
            INSERT INTO PlanillaConfirmada (Id, AlumnoId, SesionId, ConfirmadaUtc)
                VALUES (101, 101, 101, '2026-11-19 15:00:00');
            INSERT INTO PlanillaFila (PlanillaId, Orden, EjercicioCanonical, NroSerie, Reps, PesoKg, Unidad, Incompleta)
                VALUES (101, 1, 'Sentadilla', 1, 10, '40.0', 'Kg', 0);
            """);
        await db.Database.MigrateAsync();
        var sheet = await db.PlanillasConfirmadas.Include(p => p.Filas).SingleAsync();
        Assert.Null(sheet.Name);
        Assert.Null(sheet.DeletedUtc);
        Assert.Equal(40, Assert.Single(sheet.Filas).PesoKg);
        Assert.Equal(EstadoSesionCaptura.Confirmada, (await db.SesionesCaptura.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Rename_ExistingSheet_NormalizesNameAndPreservesSnapshotAndDate()
    {
        var (client, studentId) = await Login();
        using (client)
        {
            var sheet = await Seed(studentId);
            var before = await client.GetByteArrayAsync($"/api/planillas/confirmadas/{sheet.Id}/pdf?alumnoId={studentId}");
            var response = await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "  Entrenamiento de piernas  " });
            response.EnsureSuccessStatusCode();
            var updated = (await response.Content.ReadFromJsonAsync<PlanillaConfirmadaVista>())!;
            Assert.Equal("Entrenamiento de piernas", updated.Name);
            Assert.Equal(sheet.ConfirmadaUtc, updated.ConfirmadaUtc);
            Assert.Equal(40, Assert.Single(updated.Filas).PesoKg);
            var history = (await client.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!;
            Assert.Equal(updated.Name, Assert.Single(history).Name);
            Assert.Equal(DateTimeKind.Utc, history[0].ConfirmadaUtc.Kind);
            Assert.Equal(before, await client.GetByteArrayAsync($"/api/planillas/confirmadas/{sheet.Id}/pdf?alumnoId={studentId}"));
            (await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "  " })).EnsureSuccessStatusCode();
            Assert.Null(Assert.Single((await client.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!).Name);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Piernas\ny pecho")]
    [InlineData("Piernas\u2028y pecho")]
    public async Task Rename_InvalidName_RejectsWithoutChanges(string? invalidName)
    {
        var (client, studentId) = await Login();
        using (client)
        {
            var sheet = await Seed(studentId);
            var name = invalidName ?? new string('x', 121);
            var response = await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(Assert.Single((await client.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!).Name);
        }
    }

    [Fact]
    public async Task Delete_OwnSheet_HidesSnapshotAndReportsWithoutReopeningSession()
    {
        var (client, studentId) = await Login();
        using (client)
        {
            var sheet = await Seed(studentId);
            var remaining = await Seed(studentId);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/planillas/confirmadas/{sheet.Id}")).StatusCode);
            var history = (await client.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!;
            Assert.Equal(remaining.Id, Assert.Single(history).Id);
            foreach (var format in new[] { "pdf", "excel" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/planillas/confirmadas/{sheet.Id}/{format}?alumnoId={studentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/correo",
                new { alumnoId = studentId, correo = "qa@example.test", formato = "pdf" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "Otro" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/planillas/confirmadas/{sheet.Id}")).StatusCode);
            Assert.Null((await client.GetFromJsonAsync<RespuestaSesionActual>($"/api/planillas/sesion-actual/{studentId}"))!.Sesion);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
            var deleted = await db.PlanillasConfirmadas.IgnoreQueryFilters().Include(p => p.Filas).SingleAsync(p => p.Id == sheet.Id);
            Assert.NotNull(deleted.DeletedUtc);
            Assert.Equal(40, Assert.Single(deleted.Filas).PesoKg);
            Assert.Equal(EstadoSesionCaptura.Confirmada, (await db.SesionesCaptura.FindAsync(sheet.SesionId))!.Estado);
        }
    }

    [Fact]
    public async Task Changes_ForeignSheet_Return404AndLeaveOwnerHistoryUntouched()
    {
        var (owner, studentId) = await Login();
        var (other, _) = await Login("other@example.test");
        using (owner)
        using (other)
        {
            var sheet = await Seed(studentId);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "Ajeno" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/planillas/confirmadas/{sheet.Id}")).StatusCode);
            Assert.Single((await owner.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!);
        }
    }

    [Fact]
    public async Task Changes_WithoutAgreementOrCsrf_DoNotMutateSheet()
    {
        var (client, studentId) = await Login(accept: false);
        using (client)
        {
            var sheet = await Seed(studentId);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/planillas/confirmadas/{sheet.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "Piernas" })).StatusCode);
            await AuthTestClient.AcceptAgreement(client);
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/planillas/confirmadas/{sheet.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/planillas/confirmadas/{sheet.Id}/nombre", new { name = "Piernas" })).StatusCode);
            Assert.Single((await client.GetFromJsonAsync<List<PlanillaConfirmadaVista>>($"/api/planillas/historial/{studentId}"))!);
        }
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
