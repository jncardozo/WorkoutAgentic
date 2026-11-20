using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.Legal;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data;

// Contexto EF Core del dominio (arquitectura §4): SQLite WAL para el MVP,
// migraciones escritas para ser portables a Postgres (ADR S3).
public class CapturasDbContext(DbContextOptions<CapturasDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Alumno> Alumnos => Set<Alumno>();

    public DbSet<AgreementAcceptance> AgreementAcceptances => Set<AgreementAcceptance>();

    public DbSet<Ejercicio> Ejercicios => Set<Ejercicio>();

    public DbSet<SesionCaptura> SesionesCaptura => Set<SesionCaptura>();

    public DbSet<RegistroParcial> RegistrosParciales => Set<RegistroParcial>();

    public DbSet<AclaracionPendiente> AclaracionesPendientes => Set<AclaracionPendiente>();

    public DbSet<PlanillaConfirmada> PlanillasConfirmadas => Set<PlanillaConfirmada>();

    public DbSet<PlanillaFila> PlanillasFilas => Set<PlanillaFila>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().HasOne(u => u.Student).WithOne()
            .HasForeignKey<ApplicationUser>(u => u.StudentId).OnDelete(DeleteBehavior.Restrict);
        // Configuración por entidad, regla de docs/conventions/dotnet.md.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CapturasDbContext).Assembly);
    }
}
