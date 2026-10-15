using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.TokenHash);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany(x => x.SentInvitations)
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SupplierBusiness)
            .WithMany()
            .HasForeignKey(x => x.SupplierBusinessId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class VerificationRequestConfiguration : IEntityTypeConfiguration<VerificationRequest>
{
    public void Configure(EntityTypeBuilder<VerificationRequest> builder)
    {
        builder.ToTable("verification_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RequestedItemsJson).HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SupplierResponse).HasMaxLength(1000);

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany(x => x.SentVerificationRequests)
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupplierBusiness)
            .WithMany(x => x.ReceivedVerificationRequests)
            .HasForeignKey(x => x.SupplierBusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
