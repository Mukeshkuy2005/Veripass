using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class TrustScoreConfiguration : IEntityTypeConfiguration<TrustScore>
{
    public void Configure(EntityTypeBuilder<TrustScore> builder)
    {
        builder.ToTable("trust_scores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CalculationVersion).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.BusinessId).IsUnique();
    }
}

public class TrustScoreSnapshotConfiguration : IEntityTypeConfiguration<TrustScoreSnapshot>
{
    public void Configure(EntityTypeBuilder<TrustScoreSnapshot> builder)
    {
        builder.ToTable("trust_score_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CalculationVersion).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany(x => x.TrustScoreSnapshots)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Buyer)
            .WithMany()
            .HasForeignKey(x => x.BuyerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Level).HasMaxLength(20);
        builder.Property(x => x.Compliance).HasMaxLength(30);
        builder.Property(x => x.ConfigurationSource).HasMaxLength(50).HasDefaultValue("BUYER");

        builder.HasIndex(x => new { x.BusinessId, x.CreatedAt });
        builder.HasIndex(x => new { x.BuyerId, x.SupplierId, x.CalculatedAt });
    }
}
