using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkoutAgentic.Api.Data;

namespace WorkoutAgentic.Api.Features.Hosting;

internal static class HostingModule
{
    public static IServiceCollection AddAppHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks().AddCheck<SqliteReadinessCheck>("sqlite");
        services.AddHttpsRedirection(options => options.HttpsPort = 443);
        if (configuration.GetValue<bool>("Deployment:RailwayEdge"))
        {
            // Opt-in exclusivo para un entorno Railway aislado: sin TCP proxy ni
            // servicios privados no confiables. Railway reemplaza estas cabeceras
            // en su borde público; no publica IPs estables del proxy (ADR-0053).
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardedForHeaderName = "X-Real-IP";
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }
        return services;
    }

    public static WebApplication UseAppHosting(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Deployment:RailwayEdge")) app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            // La sonda interna de Railway usa HTTP. No entrega datos ni cookies.
            app.UseWhen(context => context.Request.Path != "/health", branch =>
            {
                branch.UseHsts();
                branch.UseHttpsRedirection();
            });
        }
        return app;
    }

    public static WebApplication MapAppHosting(this WebApplication app)
    {
        app.MapHealthChecks("/health").AllowAnonymous();
        if (app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists)
        {
            app.UseStaticFiles();
            // Un recurso/API inexistente nunca debe responder con HTML de la SPA.
            app.MapFallback("/api/{**path}", () => Results.NotFound());
            app.MapFallback("/health/{**path}", () => Results.NotFound());
            app.MapFallbackToFile("index.html");
        }
        else
        {
            app.MapGet("/", () => "WorkoutAgentic API");
        }
        return app;
    }
}

internal sealed class SqliteReadinessCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
