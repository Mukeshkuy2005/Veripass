using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        
        builder.Ignore(x => x.TargetType);
        builder.Ignore(x => x.TargetId);

        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).HasDefaultValue("GENERAL");
        builder.Property(x => x.ResourceType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceId).HasMaxLength(100);
        builder.Property(x => x.TargetName).HasMaxLength(250);
        builder.Property(x => x.Result).HasMaxLength(20).HasDefaultValue("SUCCESS");
        builder.Property(x => x.EventCode).HasMaxLength(50);
        
        builder.Property(x => x.OldValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.NewValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        // Foreign keys / Navigation
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimized enterprise indexes
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.Result);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.EventCode);
    }
}
