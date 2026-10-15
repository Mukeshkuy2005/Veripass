using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class ComplianceEventConfiguration : IEntityTypeConfiguration<ComplianceEvent>
{
    public void Configure(EntityTypeBuilder<ComplianceEvent> builder)
    {
        builder.ToTable("compliance_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.ComplianceEvents)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Credential)
            .WithMany(x => x.ComplianceEvents)
            .HasForeignKey(x => x.CredentialId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.BusinessId, x.CreatedAt });
    }
}

public class ComplianceRuleConfiguration : IEntityTypeConfiguration<ComplianceRule>
{
    public void Configure(EntityTypeBuilder<ComplianceRule> builder)
    {
        builder.ToTable("compliance_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RuleExpressionJson).HasColumnType("jsonb");
    }
}
