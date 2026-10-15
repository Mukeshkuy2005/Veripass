using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class TrustScore : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int OverallScore { get; set; } = 0; // 0 to 100
    public int IdentityScore { get; set; } = 0; // 0 to 100 (Weight: 25%)
    public int CredentialScore { get; set; } = 0; // 0 to 100 (Weight: 25%)
    public int ComplianceScore { get; set; } = 0; // 0 to 100 (Weight: 20%)
    public int VerificationScore { get; set; } = 0; // 0 to 100 (Weight: 20%)
    public int RiskScore { get; set; } = 100; // 0 to 100 (Penalty factor: 10%)
    
    public string CalculationVersion { get; set; } = "v1.0";
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;
}
