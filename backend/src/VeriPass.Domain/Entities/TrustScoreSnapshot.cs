using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class TrustScoreSnapshot : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int OverallScore { get; set; }
    public int IdentityScore { get; set; }
    public int CredentialScore { get; set; }
    public int ComplianceScore { get; set; }
    public int VerificationScore { get; set; }
    public int RiskScore { get; set; }
    public string CalculationVersion { get; set; } = "v1.0";
    public string Reason { get; set; } = string.Empty; // e.g. "GST_VERIFIED", "INSURANCE_EXPIRING", "RE_VERIFICATION_COMPLETE"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
