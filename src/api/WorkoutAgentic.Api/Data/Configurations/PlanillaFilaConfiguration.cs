using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

// Filas del snapshot write-once: PK (PlanillaId, Orden). Borrado en cascada
// junto a su planilla (el dependiente vive y muere con el snapshot).
public sealed class PlanillaFilaConfiguration : IEntityTypeConfiguration<PlanillaFila>
{
    public void Configure(EntityTypeBuilder<PlanillaFila> builder)
    {
        builder.ToTable("PlanillaFila");
        builder.HasKey(f => new { f.PlanillaId, f.Orden });

        builder.Property(f => f.EjercicioCanonical).HasMaxLength(120);

        builder.Property(f => f.Unidad)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? null : Enum.Parse<Unidad>(v))
            .HasMaxLength(20);

        // Borrar junto al snapshot: es un dependiente puro (sin vida propia).
        builder.HasOne(f => f.Planilla)
            .WithMany(pc => pc.Filas)
            .HasForeignKey(f => f.PlanillaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
