using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class BuyerVerificationProfileConfiguration : IEntityTypeConfiguration<BuyerVerificationProfile>
{
    public void Configure(EntityTypeBuilder<BuyerVerificationProfile> builder)
    {
        builder.ToTable("buyer_verification_profiles", t =>
        {
            t.HasCheckConstraint("CK_buyer_verification_profiles_TotalWeight", "\"TotalWeight\" = 100");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.TotalWeight).HasDefaultValue(100);

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany()
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Requirements)
            .WithOne(x => x.Profile)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BuyerOrganizationId, x.Version });
        builder.HasIndex(x => new { x.BuyerOrganizationId, x.IsDefault });
    }
}

public class CredentialRequirementConfiguration : IEntityTypeConfiguration<CredentialRequirement>
{
    public void Configure(EntityTypeBuilder<CredentialRequirement> builder)
    {
        builder.ToTable("credential_requirements", t =>
        {
            t.HasCheckConstraint("CK_credential_requirements_Weight", "\"Weight\" > 0 AND \"Weight\" <= 100");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Rules).HasMaxLength(1000);

        builder.HasOne(x => x.Profile)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CredentialType)
            .WithMany()
            .HasForeignKey(x => x.CredentialTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ProfileId, x.Type });
    }
}
