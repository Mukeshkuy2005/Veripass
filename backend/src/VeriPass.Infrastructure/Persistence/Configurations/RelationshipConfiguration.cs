using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class RelationshipConfiguration : IEntityTypeConfiguration<BuyerSupplierRelationship>
{
    public void Configure(EntityTypeBuilder<BuyerSupplierRelationship> builder)
    {
        builder.ToTable("buyer_supplier_relationships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RelationshipType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany(x => x.BuyerRelationships)
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupplierBusiness)
            .WithMany(x => x.SupplierRelationships)
            .HasForeignKey(x => x.SupplierBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BuyerOrganizationId, x.SupplierBusinessId }).IsUnique();
    }
}

public class SupplierShortlistConfiguration : IEntityTypeConfiguration<SupplierShortlist>
{
    public void Configure(EntityTypeBuilder<SupplierShortlist> builder)
    {
        builder.ToTable("shortlists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany(x => x.Shortlists)
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Shortlist)
            .HasForeignKey(x => x.ShortlistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupplierShortlistItemConfiguration : IEntityTypeConfiguration<SupplierShortlistItem>
{
    public void Configure(EntityTypeBuilder<SupplierShortlistItem> builder)
    {
        builder.ToTable("shortlist_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ShortlistId, x.BusinessId }).IsUnique();
    }
}
