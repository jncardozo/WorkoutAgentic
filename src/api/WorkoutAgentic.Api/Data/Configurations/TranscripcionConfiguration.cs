using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class TranscripcionConfiguration : IEntityTypeConfiguration<Transcripcion>
{
    public void Configure(EntityTypeBuilder<Transcripcion> builder)
    {
        builder.ToTable("Transcripcion");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.MediaId).HasMaxLength(120);
        builder.Property(t => t.Proveedor).HasMaxLength(40);

        builder.HasOne(t => t.Sesion)
            .WithMany(s => s.Transcripciones)
            .HasForeignKey(t => t.SesionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Auditoría por medio descargado.
        builder.HasIndex(t => t.MediaId);
    }
}
