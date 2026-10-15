using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class VerificationCase : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid? CredentialId { get; set; }
    public Credential? Credential { get; set; }

    public string VerificationType { get; set; } = "FULL_ONBOARDING"; // FULL_ONBOARDING, CREDENTIAL_VERIFY, RE_VERIFICATION
    public VerificationStatus Status { get; set; } = VerificationStatus.PENDING;
    public string Priority { get; set; } = "NORMAL"; // LOW, NORMAL, HIGH, URGENT
    public Guid? AssignedVerifierId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public VerificationDecision FinalDecision { get; set; } = VerificationDecision.NONE;
    public string? DecisionReason { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.LOW;
    public DateTime? EscalatedAt { get; set; }
    public User? AssignedVerifier { get; set; }

    // Navigation
    public ICollection<VerificationCheck> Checks { get; set; } = new List<VerificationCheck>();
    public ICollection<VerificationEvidence> Evidences { get; set; } = new List<VerificationEvidence>();
    public ICollection<RiskSignal> RiskSignals { get; set; } = new List<RiskSignal>();
}
