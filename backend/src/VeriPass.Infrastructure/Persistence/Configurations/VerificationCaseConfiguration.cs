using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class VerificationCaseConfiguration : IEntityTypeConfiguration<VerificationCase>
{
    public void Configure(EntityTypeBuilder<VerificationCase> builder)
    {
        builder.ToTable("verification_cases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VerificationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FinalDecision).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20).HasDefaultValue(VeriPass.Domain.Enums.RiskLevel.LOW);
        builder.Property(x => x.EscalatedAt);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.RiskLevel);
        builder.HasIndex(x => x.AssignedVerifierId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.CompletedAt);
        builder.HasIndex(x => new { x.Status, x.Priority });
        builder.HasIndex(x => new { x.AssignedVerifierId, x.Status });

        builder.HasOne(x => x.AssignedVerifier)
            .WithMany()
            .HasForeignKey(x => x.AssignedVerifierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Business)
            .WithMany(x => x.VerificationCases)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Credential)
            .WithMany(x => x.VerificationCases)
            .HasForeignKey(x => x.CredentialId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Checks)
            .WithOne(x => x.VerificationCase)
            .HasForeignKey(x => x.VerificationCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Evidences)
            .WithOne(x => x.VerificationCase)
            .HasForeignKey(x => x.VerificationCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RiskSignals)
            .WithOne(x => x.VerificationCase)
            .HasForeignKey(x => x.VerificationCaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
