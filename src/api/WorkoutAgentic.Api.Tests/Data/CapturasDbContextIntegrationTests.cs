using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Tests.Data;

// Esquema real por migración sobre archivo temporal: NUNCA depende de datos
// productivos ni de proveedores externos.
public class CapturasDbContextIntegrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"wa-db-{Guid.NewGuid():N}.db");

    private readonly DbContextOptions<CapturasDbContext> _options;

    public CapturasDbContextIntegrationTests()
    {
        _options = new DbContextOptionsBuilder<CapturasDbContext>()
            .UseSqlite($"Data Source={_dbPath};Cache=Shared")
            .Options;
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            TryDelete(_dbPath + suffix);
        }
    }

    [Fact]
    public async Task Migrate_OnTempDatabase_CreatesSchemaAndEnablesWal()
    {
        using (var db = new CapturasDbContext(_options))
        {
            await db.Database.MigrateAsync();

            // WAL (ADR S3) queda persistente en el archivo de la base.
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var mode = (string?)(await command.ExecuteScalarAsync()) ?? string.Empty;

            Assert.Contains("wal", mode, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SaveChanges_WithCapturaRoundtrip_PersistsEntitiesAndEnums()
    {
        using (var db = new CapturasDbContext(_options))
        {
            await db.Database.MigrateAsync();

            var ejercicio = new Ejercicio
            {
                NombreCanonico = "Sillón de cuadríceps",
                Apodos = ["sillón", "sillon"],
                GrupoMuscular = "Cuádriceps",
                UsaPeso = true,
            };
            var alumno = new Alumno { Nombre = "Alumno de prueba" };
            var sesion = new SesionCaptura { Alumno = alumno, UltimaActividadUtc = DateTime.UtcNow };
            sesion.Registros.Add(new RegistroParcial
            {
                Sesion = sesion,
                Ejercicio = ejercicio,
                NroSerie = 1,
                Reps = 12,
                PesoKg = 45m,
                Unidad = Unidad.Kg,
                CreadoUtc = DateTime.UtcNow,
            });

            db.AddRange(ejercicio, alumno);
            db.Add(sesion);
            await db.SaveChangesAsync();
        }

        // Lectura fresca (sin tracking del SaveChanges anterior).
        using (var db = new CapturasDbContext(_options))
        {
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();

            var found = await db.RegistrosParciales
                .Include(r => r.Ejercicio)
                .Include(r => r.Sesion)
                .ThenInclude(s => s!.Alumno)
                .SingleAsync();

            Assert.Equal("Sillón de cuadríceps", found.Ejercicio!.NombreCanonico);
            Assert.Equal(Unidad.Kg, found.Unidad);
            Assert.Equal(45m, found.PesoKg);
            Assert.Equal(12, found.Reps);
            Assert.Equal("Alumno de prueba", found.Sesion!.Alumno!.Nombre);
            Assert.Equal(EstadoSesionCaptura.EnCurso, found.Sesion.Estado);
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
        catch (IOException)
        {
            // Los artefactos -wal/-shm se sueltan al cerrar la última conexión.
        }
    }
}
