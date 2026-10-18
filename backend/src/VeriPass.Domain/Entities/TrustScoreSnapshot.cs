using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class TrustScoreSnapshot : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    // Buyer-specific weighted trust score fields
    public Guid? BuyerId { get; set; }
    public Organization? Buyer { get; set; }
    public Guid? SupplierId { get; set; }
    public Business? Supplier { get; set; }

    public int OverallScore { get; set; }
    public int Score { get; set; }
    public string Level { get; set; } = "Low"; // Low, Medium, High
    public string Compliance { get; set; } = "Incomplete"; // Compliant, Incomplete, VerificationFailed
    public int ProfileVersion { get; set; } = 1;
    public string ConfigurationSource { get; set; } = "BUYER"; // BUYER, PLATFORM_DEFAULT
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string? BreakdownJson { get; set; }

    public int IdentityScore { get; set; }
    public int CredentialScore { get; set; }
    public int ComplianceScore { get; set; }
    public int VerificationScore { get; set; }
    public int RiskScore { get; set; }
    public string CalculationVersion { get; set; } = "v1.0";
    public string Reason { get; set; } = string.Empty; // e.g. "GST_VERIFIED", "INSURANCE_EXPIRING", "RE_VERIFICATION_COMPLETE", "BUYER_CALCULATION"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

