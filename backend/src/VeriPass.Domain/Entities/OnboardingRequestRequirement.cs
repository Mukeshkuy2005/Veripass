using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class OnboardingRequestRequirement : BaseEntity
{
    public Guid OnboardingRequestId { get; set; }
    public OnboardingRequest OnboardingRequest { get; set; } = null!;

    public Guid RequirementId { get; set; }
    public OnboardingRequirement Requirement { get; set; } = null!;

    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;

    public OnboardingRequirementStatus Status { get; set; } = OnboardingRequirementStatus.PENDING;
    public string VerificationStatus { get; set; } = "PENDING"; // PENDING, VERIFIED, FAILED, WAIVED
    public Guid? VerificationResultId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedValue { get; set; }
    public string? VerificationSource { get; set; } // e.g. "GSTN Direct Portal", "MCA21 Registry", "NPCI Penny-Drop"
    public string? FailureReason { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public string? EvidenceDocumentName { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
