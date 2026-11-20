using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Auth;

namespace WorkoutAgentic.Api.Tests.Features.Alumnos;

public class AlumnosEndpointTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-auth-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestEmailSender _email = new();
    public AlumnosEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
            builder.UseSetting("Auth:PublicOrigin", "https://workout.example");
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IAccountEmailSender>(_email)));
        });
    }
    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            try { File.Delete(_dbPath + suffix); } catch (IOException) { }
    }
    private async Task<HttpClient> Client()
    {
        var client = AuthTestClient.Anonymous(_factory);
        await AuthTestClient.RefreshCsrf(client);
        return client;
    }
    private static Task<HttpResponseMessage> Register(HttpClient client, string email = "tester@example.test", string name = "Tester") =>
        client.PostAsJsonAsync("/api/alumnos", new { name, email, password = AuthTestClient.Password });
    private static async Task Login(HttpClient client, string email = "tester@example.test", string? password = null)
    {
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = password ?? AuthTestClient.Password })).EnsureSuccessStatusCode();
        await AuthTestClient.RefreshCsrf(client);
        await AuthTestClient.AcceptAgreement(client);
    }
    [Fact]
    public async Task Register_Login_Me_Logout_RevokesCopiedCookie()
    {
        using var client = await Client();
        var response = await Register(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = (await response.Content.ReadFromJsonAsync<AccountView>())!;
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = AuthTestClient.Password });
        login.EnsureSuccessStatusCode();
        var authCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("WorkoutAgentic.Auth="));
        Assert.Contains("httponly", authCookie.ToLowerInvariant());
        Assert.Contains("secure", authCookie.ToLowerInvariant());
        using var copied = AuthTestClient.Anonymous(_factory);
        copied.DefaultRequestHeaders.Add("Cookie", authCookie.Split(';')[0]);
        Assert.Equal(account, await client.GetFromJsonAsync<AccountView>("/api/auth/me"));
        await AuthTestClient.RefreshCsrf(client);
        (await client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await copied.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task DuplicateEmail_IsCaseInsensitive_AndDoesNotCreateOrphanStudent()
    {
        using var client = await Client();
        (await Register(client)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(client, "TESTER@example.test")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        Assert.Equal(1, await db.Alumnos.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.NotEqual(AuthTestClient.Password, (await db.Users.SingleAsync()).PasswordHash);
    }
    [Theory]
    [InlineData("", "a@example.test", "una frase suficientemente larga")]
    [InlineData("Tester", "incorrecto", "una frase suficientemente larga")]
    [InlineData("Tester", "a@example.test", "corta")]
    public async Task InvalidRegistration_IsRejected(string name, string email, string password)
    {
        using var client = await Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/alumnos", new { name, email, password })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<CapturasDbContext>().Alumnos.ToListAsync());
    }
    [Fact]
    public async Task MissingCsrf_CannotRegisterOrLogout()
    {
        using var client = AuthTestClient.Anonymous(_factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(client)).StatusCode);
        await AuthTestClient.RefreshCsrf(client);
        (await Register(client)).EnsureSuccessStatusCode();
        await Login(client);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/planillas/sesion-actual/1")]
    [InlineData("/api/planillas/historial/1")]
    [InlineData("/api/planillas/confirmadas/1/pdf?alumnoId=1")]
    public async Task AnonymousRead_RequiresLogin(string url)
    {
        using var client = await Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }
    [Fact]
    public async Task TwoStudents_CannotAccessOrChangeEachOthersTraining()
    {
        using var first = await Client();
        using var second = await Client();
        var a = (await (await Register(first, "iphone@example.test", "iPhone")).Content.ReadFromJsonAsync<AccountView>())!;
        var b = (await (await Register(second, "samsung@example.test", "Samsung")).Content.ReadFromJsonAsync<AccountView>())!;
        await Login(first, a.Email); await Login(second, b.Email);
        int rowId; int sheetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
            var session = new SesionCaptura { AlumnoId = a.StudentId, Estado = EstadoSesionCaptura.EnCurso, UltimaActividadUtc = DateTime.UtcNow };
            var row = new RegistroParcial { Sesion = session, EjercicioCrudo = "Press", EjercicioCanonico = "Press", NroSerie = 1, Reps = 10, PesoKg = 40, Unidad = Unidad.Kg };
            var sheet = new PlanillaConfirmada { AlumnoId = a.StudentId, Sesion = session, ConfirmadaUtc = DateTime.UtcNow };
            db.AddRange(row, sheet); await db.SaveChangesAsync();
            rowId = row.Id; sheetId = sheet.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync($"/api/planillas/sesion-actual/{a.StudentId}")).StatusCode);
        var own = await second.GetFromJsonAsync<WorkoutAgentic.Api.Features.PlanillaApi.RespuestaSesionActual>($"/api/planillas/sesion-actual/{b.StudentId}");
        Assert.Null(own!.Sesion);
        foreach (var url in new[] { $"/api/planillas/sesion-actual/{a.StudentId}", $"/api/planillas/historial/{a.StudentId}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await second.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.PostAsJsonAsync($"/api/capturas/{a.StudentId}/texto", new { texto = "Press 3 series" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.PostAsJsonAsync("/api/planillas/confirmar", new { alumnoId = a.StudentId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.PostAsJsonAsync("/api/planillas/filas", new { alumnoId = a.StudentId, nroSerie = 2, reps = 10, pesoKg = 40, ejercicioCrudo = "Press" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.DeleteAsync($"/api/planillas/sesion-actual/{a.StudentId}/filas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.PutAsJsonAsync($"/api/planillas/sesion-actual/{a.StudentId}/filas", new { rows = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.DeleteAsync($"/api/planillas/filas/{rowId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PutAsJsonAsync($"/api/planillas/filas/{rowId}", new { nroSerie = 1, reps = 5 })).StatusCode);
        foreach (var format in new[] { "pdf", "excel" })
            Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/planillas/confirmadas/{sheetId}/{format}?alumnoId={b.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsJsonAsync($"/api/planillas/confirmadas/{sheetId}/correo", new { alumnoId = b.StudentId, correo = "test@example.test", formato = "pdf" })).StatusCode);
        using var check = _factory.Services.CreateScope();
        Assert.Equal(40, (await check.ServiceProvider.GetRequiredService<CapturasDbContext>().RegistrosParciales.SingleAsync()).PesoKg);
    }
    [Fact]
    public async Task FailedLogin_LocksAccount()
    {
        using var client = await Client();
        (await Register(client)).EnsureSuccessStatusCode();
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "tester@example.test", password = "clave incorrecta" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "tester@example.test", password = AuthTestClient.Password })).StatusCode);
    }
    [Fact]
    public async Task Recovery_ResetsPassword_InvalidatesSessions_AndTokenCannotBeReused()
    {
        using var client = await Client();
        (await Register(client)).EnsureSuccessStatusCode(); await Login(client);
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.test" });
        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "tester@example.test" });
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(_email.Url!).Query);
        using var resetClient = await Client();
        var command = new { email = "tester@example.test", token = query["token"].ToString(), password = "una nueva frase de prueba" };
        Assert.Equal(HttpStatusCode.NoContent, (await resetClient.PostAsJsonAsync("/api/auth/reset-password", command)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await resetClient.PostAsJsonAsync("/api/auth/reset-password", command)).StatusCode);
        await Login(resetClient, password: command.password);
    }
    [Theory]
    [InlineData("/api/capturas/1/texto")]
    [InlineData("/api/planillas/confirmar")]
    [InlineData("/api/planillas/filas")]
    [InlineData("/api/planillas/confirmadas/1/correo")]
    public async Task AnonymousMutation_RequiresLoginEvenWithCsrf(string url)
    {
        using var client = await Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(url, new { })).StatusCode);
    }

    [Fact]
    public async Task NewRegistration_DoesNotClaimHistoricalStudent()
    {
        using var client = await Client();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
            db.Alumnos.Add(new Alumno { Nombre = "Demo histórico" });
            await db.SaveChangesAsync();
        }
        var response = await Register(client);
        var account = (await response.Content.ReadFromJsonAsync<AccountView>())!;
        Assert.NotEqual(1, account.StudentId);
        using var check = _factory.Services.CreateScope();
        Assert.Equal("Demo histórico", (await check.ServiceProvider.GetRequiredService<CapturasDbContext>().Alumnos.FindAsync(1))!.Nombre);
    }

    [Fact]
    public async Task AccountRequests_AreRateLimited()
    {
        using var client = await Client();
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.test" })).StatusCode);
    }

    [Fact]
    public async Task AbsoluteSessionExpiry_RejectsOtherwiseValidCookie()
    {
        using var client = await Client();
        (await Register(client)).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "tester@example.test", password = AuthTestClient.Password });
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("WorkoutAgentic.Auth="));
        var value = cookie.Split(';')[0]["WorkoutAgentic.Auth=".Length..];
        var options = _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(value)!;
        ticket.Properties.Items["absolute-expiry"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        using var expired = AuthTestClient.Anonymous(_factory);
        expired.DefaultRequestHeaders.Add("Cookie", $"WorkoutAgentic.Auth={options.TicketDataFormat.Protect(ticket)}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync("/api/auth/me")).StatusCode);
    }

    private sealed class TestEmailSender : IAccountEmailSender
    {
        public string? Url { get; private set; }
        public Task SendResetAsync(string email, string url, CancellationToken cancellationToken) { Url = url; return Task.CompletedTask; }
    }
}
