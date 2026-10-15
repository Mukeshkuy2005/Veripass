using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Persistence.Configurations;

public class OnboardingRequestConfiguration : IEntityTypeConfiguration<OnboardingRequest>
{
    public void Configure(EntityTypeBuilder<OnboardingRequest> builder)
    {
        builder.ToTable("onboarding_requests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Purpose).HasMaxLength(250).IsRequired();
        builder.Property(x => x.BusinessCategory).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ProcurementDepartment).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ExpectedRelationship).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.ActionRequiredReason).HasMaxLength(1000);
        builder.Property(x => x.ErpVendorCode).HasMaxLength(60);
        builder.Property(x => x.PaymentTerms).HasMaxLength(100);
        builder.Property(x => x.TargetPlant).HasMaxLength(150);

        builder.HasOne(x => x.BuyerOrganization)
            .WithMany(x => x.BuyerOnboardingRequests)
            .HasForeignKey(x => x.BuyerOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupplierBusiness)
            .WithMany(x => x.OnboardingRequests)
            .HasForeignKey(x => x.SupplierBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RejectedByUser)
            .WithMany()
            .HasForeignKey(x => x.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Requirements)
            .WithOne(x => x.OnboardingRequest)
            .HasForeignKey(x => x.OnboardingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BuyerOrganizationId);
        builder.HasIndex(x => x.SupplierBusinessId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => new { x.BuyerOrganizationId, x.SupplierBusinessId });
    }
}

public class OnboardingRequirementConfiguration : IEntityTypeConfiguration<OnboardingRequirement>
{
    public void Configure(EntityTypeBuilder<OnboardingRequirement> builder)
    {
        builder.ToTable("onboarding_requirements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class OnboardingRequestRequirementConfiguration : IEntityTypeConfiguration<OnboardingRequestRequirement>
{
    public void Configure(EntityTypeBuilder<OnboardingRequestRequirement> builder)
    {
        builder.ToTable("onboarding_request_requirements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequirementCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RequirementName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.VerificationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.VerificationSource).HasMaxLength(150);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.EvidenceDocumentName).HasMaxLength(200);

        builder.HasOne(x => x.Requirement)
            .WithMany(x => x.RequestRequirements)
            .HasForeignKey(x => x.RequirementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OnboardingRequestId);
        builder.HasIndex(x => x.RequirementId);
        builder.HasIndex(x => x.Status);
    }
}
