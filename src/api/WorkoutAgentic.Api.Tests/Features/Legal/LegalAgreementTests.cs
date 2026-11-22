using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.Legal;

namespace WorkoutAgentic.Api.Tests.Features.Legal;

public sealed class LegalAgreementTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-legal-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public LegalAgreementTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}"));
    }

    private async Task<(HttpClient Client, AccountView Account)> Login(string email = "qa@example.test")
    {
        var client = AuthTestClient.Anonymous(_factory);
        await AuthTestClient.RefreshCsrf(client);
        var register = await client.PostAsJsonAsync("/api/alumnos", new { name = "QA", email, password = AuthTestClient.Password });
        register.EnsureSuccessStatusCode();
        var account = (await register.Content.ReadFromJsonAsync<AccountView>())!;
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = AuthTestClient.Password })).EnsureSuccessStatusCode();
        await AuthTestClient.RefreshCsrf(client);
        return (client, account);
    }

    [Fact]
    public async Task Agreement_AnonymousRead_ShowsCompleteDocumentWithoutPlaceholders()
    {
        using var client = AuthTestClient.Anonymous(_factory);
        var response = await client.GetAsync("/api/legal/agreement");
        var document = (await response.Content.ReadFromJsonAsync<AgreementView>())!;
        Assert.True(document.Available);
        Assert.Contains("10. Aceptación y copia", document.Text);
        Assert.Contains($"Versión {document.Version}", document.Text);
        Assert.DoesNotContain("{{", document.Text);
        Assert.DoesNotContain("COMPLETAR", document.Text);
        Assert.Equal(64, document.Hash.Length);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Training_WithoutAcceptance_BlocksAllOperationTypesAndKeepsAccountAvailable()
    {
        var (client, account) = await Login();
        using (client)
        {
            Assert.False((await client.GetFromJsonAsync<AccountView>("/api/auth/me"))!.AgreementAccepted);
            var operations = new (HttpMethod Method, string Url, object? Body)[]
            {
                (HttpMethod.Get, $"/api/planillas/sesion-actual/{account.StudentId}", null),
                (HttpMethod.Get, $"/api/planillas/historial/{account.StudentId}", null),
                (HttpMethod.Get, "/api/planillas/correo/estado", null),
                (HttpMethod.Get, $"/api/planillas/confirmadas/1/pdf?alumnoId={account.StudentId}", null),
                (HttpMethod.Get, $"/api/planillas/confirmadas/1/excel?alumnoId={account.StudentId}", null),
                (HttpMethod.Post, $"/api/capturas/{account.StudentId}/texto", new { texto = "Press 3 series" }),
                (HttpMethod.Post, "/api/planillas/confirmar", new { alumnoId = account.StudentId }),
                (HttpMethod.Post, "/api/planillas/filas", new { alumnoId = account.StudentId, nroSerie = 1, reps = 10, pesoKg = 40, ejercicioCrudo = "Press" }),
                (HttpMethod.Put, "/api/planillas/filas/1", new { nroSerie = 1, reps = 5 }),
                (HttpMethod.Delete, "/api/planillas/filas/1", null),
                (HttpMethod.Post, "/api/planillas/confirmadas/1/correo", new { alumnoId = account.StudentId, correo = "a@example.test", formato = "pdf" }),
            };
            foreach (var operation in operations)
            {
                using var request = new HttpRequestMessage(operation.Method, operation.Url);
                if (operation.Body is not null) request.Content = JsonContent.Create(operation.Body);
                var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Contains("agreement_required", await response.Content.ReadAsStringAsync());
            }
            (await client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Accept_RefusedOrStaleDocument_DoesNotPersist(bool accepted, bool staleVersion, bool staleHash)
    {
        var (client, _) = await Login();
        using (client)
        {
            var document = (await client.GetFromJsonAsync<AgreementView>("/api/legal/agreement"))!;
            var response = await client.PostAsJsonAsync("/api/legal/accept",
                new AcceptAgreementRequest(staleVersion ? "old" : document.Version, staleHash ? "other-hash" : document.Hash, accepted));
            Assert.Equal(staleVersion || staleHash ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, response.StatusCode);
            using var scope = _factory.Services.CreateScope();
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<CapturasDbContext>().AgreementAcceptances.ToListAsync());
        }
    }

    [Fact]
    public async Task Accept_PersistsSnapshotOnce_UnlocksOnlyOwnAccountAndSurvivesLogin()
    {
        var (first, account) = await Login();
        var (second, _) = await Login("other@example.test");
        using (first) using (second)
        {
            var document = (await first.GetFromJsonAsync<AgreementView>("/api/legal/agreement"))!;
            var before = DateTime.UtcNow;
            await AuthTestClient.AcceptAgreement(first);
            await AuthTestClient.AcceptAgreement(first);
            Assert.True((await first.GetFromJsonAsync<AccountView>("/api/auth/me"))!.AgreementAccepted);
            Assert.False((await second.GetFromJsonAsync<AccountView>("/api/auth/me"))!.AgreementAccepted);
            Assert.Equal(HttpStatusCode.OK, (await first.GetAsync($"/api/planillas/sesion-actual/{account.StudentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/legal/receipt")).StatusCode);
            using (var scope = _factory.Services.CreateScope())
            {
                var acceptance = await scope.ServiceProvider.GetRequiredService<CapturasDbContext>().AgreementAcceptances.SingleAsync();
                Assert.Equal(document.Text, acceptance.DocumentText);
                Assert.Equal(document.Hash, acceptance.DocumentHash);
                Assert.Equal(account.Email, acceptance.ParticipantEmail);
                Assert.InRange(acceptance.AcceptedAtUtc, before, DateTime.UtcNow);
            }
            (await first.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
            await AuthTestClient.RefreshCsrf(first);
            (await first.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = AuthTestClient.Password })).EnsureSuccessStatusCode();
            Assert.True((await first.GetFromJsonAsync<AccountView>("/api/auth/me"))!.AgreementAccepted);
            var receipt = await first.GetAsync("/api/legal/receipt");
            Assert.Contains("attachment", receipt.Content.Headers.ContentDisposition!.ToString());
            Assert.Contains(document.Text, await receipt.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Accept_RequiresAuthenticationAndCsrf()
    {
        using var anonymous = AuthTestClient.Anonymous(_factory);
        await AuthTestClient.RefreshCsrf(anonymous);
        var document = (await anonymous.GetFromJsonAsync<AgreementView>("/api/legal/agreement"))!;
        var command = new AcceptAgreementRequest(document.Version, document.Hash, true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/legal/accept", command)).StatusCode);
        var (client, _) = await Login();
        using (client)
        {
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/legal/accept", command)).StatusCode);
        }
    }

    [Fact]
    public async Task ChangedDocument_RequiresAcceptanceAgainAndRetainsOldReceipt()
    {
        var (client, account) = await Login();
        using (client) await AuthTestClient.AcceptAgreement(client);
        using var changed = _factory.WithWebHostBuilder(builder => builder.UseSetting("Legal:OwnerAddress", "Otro domicilio de prueba"));
        using var nextClient = AuthTestClient.Anonymous(changed);
        await AuthTestClient.RefreshCsrf(nextClient);
        (await nextClient.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = AuthTestClient.Password })).EnsureSuccessStatusCode();
        await AuthTestClient.RefreshCsrf(nextClient);
        Assert.False((await nextClient.GetFromJsonAsync<AccountView>("/api/auth/me"))!.AgreementAccepted);
        Assert.Equal(HttpStatusCode.Forbidden, (await nextClient.GetAsync($"/api/planillas/sesion-actual/{account.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await nextClient.GetAsync("/api/legal/receipt")).StatusCode);
        await AuthTestClient.AcceptAgreement(nextClient);
        using var scope = changed.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CapturasDbContext>().AgreementAcceptances.CountAsync());
    }

    [Fact]
    public async Task MissingOwnerConfiguration_DisablesAcceptance()
    {
        using var unavailable = _factory.WithWebHostBuilder(builder => builder.UseSetting("Legal:OwnerName", ""));
        using var client = AuthTestClient.Anonymous(unavailable);
        await AuthTestClient.RefreshCsrf(client);
        (await client.PostAsJsonAsync("/api/alumnos", new { name = "QA", email = "unavailable@example.test", password = AuthTestClient.Password })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login", new { email = "unavailable@example.test", password = AuthTestClient.Password })).EnsureSuccessStatusCode();
        await AuthTestClient.RefreshCsrf(client);
        var document = (await client.GetFromJsonAsync<AgreementView>("/api/legal/agreement"))!;
        Assert.False(document.Available);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/legal/accept",
            new AcceptAgreementRequest(document.Version, document.Hash, true))).StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            try { File.Delete(_dbPath + suffix); } catch (IOException) { }
    }
}
