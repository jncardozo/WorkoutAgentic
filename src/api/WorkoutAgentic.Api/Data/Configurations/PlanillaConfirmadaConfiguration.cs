using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class PlanillaConfirmadaConfiguration : IEntityTypeConfiguration<PlanillaConfirmada>
{
    public void Configure(EntityTypeBuilder<PlanillaConfirmada> builder)
    {
        builder.ToTable("PlanillaConfirmada");
        builder.HasKey(pc => pc.Id);

        builder.HasOne(pc => pc.Alumno)
            .WithMany(a => a.PlanillasConfirmadas)
            .HasForeignKey(pc => pc.AlumnoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Snapshot 1:1 con la sesión confirmada.
        builder.HasOne(pc => pc.Sesion)
            .WithOne(s => s.PlanillaConfirmada!)
            .HasForeignKey<PlanillaConfirmada>(pc => pc.SesionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un snapshot por sesión.
        builder.HasIndex(pc => pc.SesionId).IsUnique();
        builder.HasIndex(pc => pc.AlumnoId);
    }
}
