namespace VeriPass.Application.Common.Interfaces;

public class TrustScoreCalculationResult
{
    public int OverallScore { get; set; }
    public int IdentityScore { get; set; }
    public int CredentialScore { get; set; }
    public int ComplianceScore { get; set; }
    public int VerificationScore { get; set; }
    public int RiskScore { get; set; }
    public string Version { get; set; } = "v1.0";
    public string SummaryReason { get; set; } = string.Empty;
}

public interface ITrustScoreCalculator
{
    Task<TrustScoreCalculationResult> CalculateAsync(Guid businessId, CancellationToken cancellationToken = default);
}
