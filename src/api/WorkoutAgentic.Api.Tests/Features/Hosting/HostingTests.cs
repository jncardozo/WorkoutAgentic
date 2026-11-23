using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;
using WorkoutAgentic.Api.Features.Auth;

namespace WorkoutAgentic.Api.Tests.Features.Hosting;

public sealed class HostingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wa-hosting-{Guid.NewGuid():N}");
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public HostingTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "index.html"), "<!doctype html><html><body>SPA de prueba</body></html>");
        File.WriteAllText(Path.Combine(_directory, "main-test.js"), "console.log('SPA');");
    }

    private WebApplicationFactory<Program> CreateFactory(bool railwayEdge = false)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={Path.Combine(_directory, Guid.NewGuid() + ".db")}");
            builder.UseSetting("Auth:DataProtectionKeysPath", Path.Combine(_directory, "keys"));
            builder.UseSetting("Deployment:RailwayEdge", railwayEdge.ToString());
            builder.ConfigureServices(services =>
            {
                foreach (var environment in services.Where(s => s.ServiceType == typeof(IWebHostEnvironment))
                    .Select(s => s.ImplementationInstance).OfType<IWebHostEnvironment>())
                {
                    environment.WebRootPath = _directory;
                    environment.WebRootFileProvider = new PhysicalFileProvider(_directory);
                }
            });
        });
        _factories.Add(factory);
        return factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/restablecer-clave")]
    public async Task Spa_DirectNavigation_ServesIndexWithoutCaching(string path)
    {
        using var client = AuthTestClient.Anonymous(CreateFactory());
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("SPA de prueba", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/no-existe")]
    [InlineData("/api/no-existe.json")]
    [InlineData("/health/no-existe")]
    [InlineData("/main-missing.js")]
    public async Task Spa_MissingApiOrAsset_Returns404WithoutHtml(string path)
    {
        using var client = AuthTestClient.Anonymous(CreateFactory());
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("SPA de prueba", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_InternalHttp_ReadyWithoutRedirectOrCookies()
    {
        using var client = CreateFactory().CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://healthcheck.railway.app"), AllowAutoRedirect = false });
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Https_ForwardedHeaderWithoutOptIn_DoesNotBypassRedirect()
    {
        using var client = CreateFactory().CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var response = await client.GetAsync("/login");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location!.Scheme);
    }

    [Fact]
    public async Task Railway_PublicHttpsOverInternalHttp_RegistersAndLogsInWithSecureCookies()
    {
        using var client = CreateFactory(true).CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://qa.example.test"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Real-IP", "203.0.113.23");
        await AuthTestClient.RefreshCsrf(client);
        (await client.PostAsJsonAsync("/api/alumnos", new RegisterRequest("QA", "hosting@example.test", AuthTestClient.Password)))
            .EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("hosting@example.test", AuthTestClient.Password));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("WorkoutAgentic.Auth="));
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.NotNull(await client.GetFromJsonAsync<AccountView>("/api/auth/me"));

        // La conexión real al contenedor es HTTP, el esquema público llega del borde.
        using var internalClient = CreateFactory(true).CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://qa.example.test"), AllowAutoRedirect = false });
        internalClient.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var csrf = await internalClient.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        Assert.All(csrf.Headers.GetValues("Set-Cookie"), value => Assert.Contains("secure", value));
    }

    public void Dispose()
    {
        foreach (var factory in _factories) factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
