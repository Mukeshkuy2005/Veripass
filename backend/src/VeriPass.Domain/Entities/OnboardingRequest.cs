using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class OnboardingRequest : AuditableEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public Guid SupplierBusinessId { get; set; }
    public Business SupplierBusiness { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public OnboardingStatus Status { get; set; } = OnboardingStatus.NEW;

    public string Purpose { get; set; } = string.Empty;
    public string BusinessCategory { get; set; } = string.Empty;
    public string ProcurementDepartment { get; set; } = string.Empty;
    public string ExpectedRelationship { get; set; } = "Direct Production Supplier";
    public string? Message { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Procurement Approval Decision
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    // Rejection Details
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public User? RejectedByUser { get; set; }
    public string? RejectionReason { get; set; }

    // Action Required Reason
    public string? ActionRequiredReason { get; set; }

    // ERP Hand-off
    public string? ErpVendorCode { get; set; }
    public string? PaymentTerms { get; set; }
    public string? TargetPlant { get; set; }

    // Navigation
    public ICollection<OnboardingRequestRequirement> Requirements { get; set; } = new List<OnboardingRequestRequirement>();
}
