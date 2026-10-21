using Microsoft.EntityFrameworkCore;

namespace WorkoutAgentic.Api.Data;

// Registro del slice de base de datos.
public static class CapturasDbModule
{
    public static IServiceCollection AddCapturasDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sqlite");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Sqlite (appsettings.json o variable de entorno).");
        }

        services.AddDbContext<CapturasDbContext>(options => options.UseSqlite(connectionString));
        services.AddHostedService<DbInitializer>();

        return services;
    }
}
