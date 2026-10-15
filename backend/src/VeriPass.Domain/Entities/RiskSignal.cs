using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class RiskSignal : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid? VerificationCaseId { get; set; }
    public VerificationCase? VerificationCase { get; set; }

    public string SignalType { get; set; } = string.Empty; // DOCUMENT_TAMPERING, IDENTITY_MISMATCH, DUPLICATE_CREDENTIAL, EXPIRED_DOCUMENT, ADDRESS_MISMATCH, UNUSUAL_ACTIVITY
    public RiskLevel Severity { get; set; } = RiskLevel.MEDIUM;
    public int ScoreImpact { get; set; } = -10; // Negative points applied to trust score
    public string Description { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; } // JSONB
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, RESOLVED, DISMISSED, FALSE_POSITIVE
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}
