using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkoutAgentic.Api.Features.Auth;
using WorkoutAgentic.Api.Features.Legal;

namespace WorkoutAgentic.Api.Data.Configurations;

public sealed class AgreementAcceptanceConfiguration : IEntityTypeConfiguration<AgreementAcceptance>
{
    public void Configure(EntityTypeBuilder<AgreementAcceptance> builder)
    {
        builder.ToTable("AgreementAcceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.Version).HasMaxLength(80);
        builder.Property(a => a.DocumentHash).HasMaxLength(64);
        builder.Property(a => a.ParticipantName).HasMaxLength(120);
        builder.Property(a => a.ParticipantEmail).HasMaxLength(180);
        builder.HasIndex(a => new { a.UserId, a.DocumentHash }).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
