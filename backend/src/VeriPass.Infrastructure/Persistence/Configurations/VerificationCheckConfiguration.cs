using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class VerificationCheckConfiguration : IEntityTypeConfiguration<VerificationCheck>
{
    public void Configure(EntityTypeBuilder<VerificationCheck> builder)
    {
        builder.ToTable("verification_checks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CheckType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.ConfidenceScore).HasPrecision(5, 2);
    }
}

public class VerificationEvidenceConfiguration : IEntityTypeConfiguration<VerificationEvidence>
{
    public void Configure(EntityTypeBuilder<VerificationEvidence> builder)
    {
        builder.ToTable("verification_evidence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EvidenceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MatchStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ConfidenceScore).HasPrecision(5, 2);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.HasOne(x => x.Document)
            .WithMany(x => x.VerificationEvidences)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class RiskSignalConfiguration : IEntityTypeConfiguration<RiskSignal>
{
    public void Configure(EntityTypeBuilder<RiskSignal> builder)
    {
        builder.ToTable("risk_signals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SignalType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EvidenceJson).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.RiskSignals)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
