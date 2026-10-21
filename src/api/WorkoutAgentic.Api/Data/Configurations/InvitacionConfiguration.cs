using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Data.Entities;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class InvitacionConfiguration : IEntityTypeConfiguration<Invitacion>
{
    public void Configure(EntityTypeBuilder<Invitacion> builder)
    {
        builder.ToTable("Invitacion");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Codigo).HasMaxLength(50);

        // El código se consume por URL exacta: único.
        builder.HasIndex(i => i.Codigo).IsUnique();

        builder.HasOne(i => i.Profesor)
            .WithMany(p => p.Invitaciones)
            .HasForeignKey(i => i.ProfesorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Alumno)
            .WithMany()
            .HasForeignKey(i => i.AlumnoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
