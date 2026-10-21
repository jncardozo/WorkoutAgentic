using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

// 1:1 con la sesión: la PK ES la FK (una viva por sesión).
public sealed class AclaracionPendienteConfiguration : IEntityTypeConfiguration<AclaracionPendiente>
{
    public void Configure(EntityTypeBuilder<AclaracionPendiente> builder)
    {
        builder.ToTable("AclaracionPendiente");
        builder.HasKey(a => a.SesionId);

        builder.Property(a => a.Campo).HasMaxLength(30);

        builder.HasOne(a => a.Sesion)
            .WithOne(s => s.AclaracionPendiente!)
            .HasForeignKey<AclaracionPendiente>(a => a.SesionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
