using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class SesionCapturaConfiguration : IEntityTypeConfiguration<SesionCaptura>
{
    public void Configure(EntityTypeBuilder<SesionCaptura> builder)
    {
        builder.ToTable("SesionCaptura");
        builder.HasKey(s => s.Id);

        // Estado del diálogo como texto: portable a Postgres (ADR S3).
        builder.Property(s => s.Estado)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(s => s.AlumnoId);
        builder.HasIndex(s => s.Estado);

        builder.HasOne(s => s.Alumno)
            .WithMany(a => a.Sesiones)
            .HasForeignKey(s => s.AlumnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.EjercicioActivo)
            .WithMany(e => e.SesionesActivas)
            .HasForeignKey(s => s.EjercicioActivoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
