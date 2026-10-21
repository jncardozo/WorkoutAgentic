using Microsoft.EntityFrameworkCore;

namespace WorkoutAgentic.Api.Data;

// Arranque de la base: establece journal_mode=WAL (ADR S3 — Microsoft.Data.Sqlite
// no parsea UseWAL desde la connection string) y aplica las migrations pendientes.
// La PRAGMA es persistente en el archivo de la base: basta una vez por arranque.
internal sealed class DbInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DbInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapturasDbContext>();

        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Base de datos lista: SQLite en modo WAL y migrations aplicadas.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
