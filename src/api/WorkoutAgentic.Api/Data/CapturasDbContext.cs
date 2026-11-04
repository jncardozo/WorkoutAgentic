using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data;

// Contexto EF Core del dominio (arquitectura §4): SQLite WAL para el MVP,
// migraciones escritas para ser portables a Postgres (ADR S3).
public class CapturasDbContext(DbContextOptions<CapturasDbContext> options) : DbContext(options)
{
    public DbSet<Alumno> Alumnos => Set<Alumno>();

    public DbSet<Ejercicio> Ejercicios => Set<Ejercicio>();

    public DbSet<SesionCaptura> SesionesCaptura => Set<SesionCaptura>();

    public DbSet<RegistroParcial> RegistrosParciales => Set<RegistroParcial>();

    public DbSet<AclaracionPendiente> AclaracionesPendientes => Set<AclaracionPendiente>();

    public DbSet<PlanillaConfirmada> PlanillasConfirmadas => Set<PlanillaConfirmada>();

    public DbSet<PlanillaFila> PlanillasFilas => Set<PlanillaFila>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración por entidad, regla de docs/conventions/dotnet.md.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CapturasDbContext).Assembly);
    }
}
