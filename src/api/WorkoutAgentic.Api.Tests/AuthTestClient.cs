using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.Legal;

namespace WorkoutAgentic.Api.Tests;

internal static class AuthTestClient
{
    internal const string Password = "una frase de prueba segura";

    internal static HttpClient Anonymous(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    internal static async Task RefreshCsrf(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    internal static async Task AcceptAgreement(HttpClient client)
    {
        var document = (await client.GetFromJsonAsync<AgreementView>("/api/legal/agreement"))!;
        (await client.PostAsJsonAsync("/api/legal/accept", new AcceptAgreementRequest(document.Version, document.Hash, true)))
            .EnsureSuccessStatusCode();
    }

    // Domain endpoint regressions still exercise real cookies and antiforgery.
    // Link a test identity to the existing seeded student without changing data.
    internal static async Task<HttpClient> ForSeededStudent(WebApplicationFactory<Program> factory)
    {
        var client = Anonymous(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync("domain@example.test") is null)
            {
                var student = await db.Alumnos.OrderBy(a => a.Id).FirstOrDefaultAsync() ?? new Alumno { Nombre = "Prueba autenticada" };
                var result = await users.CreateAsync(new ApplicationUser
                { Email = "domain@example.test", UserName = "domain@example.test", Student = student }, Password);
                Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));
            }
        }
        await RefreshCsrf(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "domain@example.test", password = Password });
        login.EnsureSuccessStatusCode();
        await RefreshCsrf(client);
        await AcceptAgreement(client);
        return client;
    }
}
