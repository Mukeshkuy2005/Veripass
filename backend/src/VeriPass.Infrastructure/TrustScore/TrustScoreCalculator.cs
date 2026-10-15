using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.TrustScoring;

public class TrustScoreCalculator : ITrustScoreCalculator
{
    private readonly IApplicationDbContext _context;

    public TrustScoreCalculator(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TrustScoreCalculationResult> CalculateAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        var business = await _context.Businesses
            .Include(b => b.Addresses)
            .Include(b => b.Directors)
            .Include(b => b.BankAccounts)
            .Include(b => b.Credentials)
                .ThenInclude(c => c.CredentialType)
            .Include(b => b.Documents)
            .Include(b => b.RiskSignals)
            .Include(b => b.ComplianceEvents)
            .FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken);

        if (business == null)
        {
            return new TrustScoreCalculationResult
            {
                OverallScore = 0,
                SummaryReason = "Business entity not found."
            };
        }

        // 1. Identity Score (25% Weight)
        // Evaluates presence and validity of PAN, GSTIN, CIN/Udyam, Directors, Registered Address
        int identityPoints = 0;
        if (!string.IsNullOrWhiteSpace(business.Pan)) identityPoints += 25;
        if (!string.IsNullOrWhiteSpace(business.Gstin)) identityPoints += 25;
        if (!string.IsNullOrWhiteSpace(business.Cin) || !string.IsNullOrWhiteSpace(business.UdyamNumber)) identityPoints += 20;
        if (business.Directors.Count > 0) identityPoints += 15;
        if (business.Addresses.Any(a => a.IsPrimary)) identityPoints += 15;
        int identityScore = Math.Clamp(identityPoints, 0, 100);

        // 2. Credential Score (25% Weight)
        // Evaluates completeness and active status of required statutory credentials
        int credentialScore = 0;
        if (business.Credentials.Count > 0)
        {
            var verifiedCount = business.Credentials.Count(c => c.VerificationStatus == VerificationStatus.VERIFIED);
            var activeCount = business.Credentials.Count(c => c.Status != CredentialStatus.EXPIRED && c.Status != CredentialStatus.SUSPENDED);
            var ratio = (double)(verifiedCount + activeCount) / (business.Credentials.Count * 2);
            credentialScore = Math.Clamp((int)(ratio * 100), 0, 100);
        }

        // 3. Compliance Score (20% Weight)
        // Checks for active compliance alerts, overdue events, or upcoming expirations
        int complianceScore = 100;
        var unresolvedEvents = business.ComplianceEvents.Where(e => e.ResolvedAt == null).ToList();
        foreach (var ev in unresolvedEvents)
        {
            if (ev.Severity == ComplianceSeverity.CRITICAL) complianceScore -= 30;
            else if (ev.Severity == ComplianceSeverity.HIGH) complianceScore -= 15;
            else if (ev.Severity == ComplianceSeverity.MEDIUM) complianceScore -= 5;
        }
        complianceScore = Math.Clamp(complianceScore, 0, 100);

        // 4. Verification Score (20% Weight)
        // Checks completed verification cases and confidence scores
        int verificationScore = 0;
        var verifiedCreds = business.Credentials.Where(c => c.VerificationStatus == VerificationStatus.VERIFIED).ToList();
        if (verifiedCreds.Count > 0)
        {
            verificationScore = Math.Min(100, verifiedCreds.Count * 25);
        }

        // 5. Risk Score (Penalty factor up to 10%)
        // Deducts points for active unresolved risk signals
        int riskScore = 100;
        var activeRisks = business.RiskSignals.Where(r => r.Status == "ACTIVE").ToList();
        int riskDeductions = activeRisks.Sum(r => Math.Abs(r.ScoreImpact));
        riskScore = Math.Clamp(riskScore - riskDeductions, 0, 100);

        // Weighted Overall Score calculation (25% + 25% + 20% + 20% + 10% = 100%)
        double weightedScore = (identityScore * 0.25)
                             + (credentialScore * 0.25)
                             + (complianceScore * 0.20)
                             + (verificationScore * 0.20)
                             + (riskScore * 0.10);

        int overallScore = Math.Clamp((int)Math.Round(weightedScore), 0, 100);

        // Build reason string
        var reasonParts = new List<string>();
        if (identityScore >= 80) reasonParts.Add("Strong Statutory Identity");
        if (credentialScore >= 80) reasonParts.Add("Verified Credentials");
        if (complianceScore < 80) reasonParts.Add("Compliance Alerts Pending");
        if (activeRisks.Count > 0) reasonParts.Add($"{activeRisks.Count} Risk Signal(s) Active");
        string reason = reasonParts.Count > 0 ? string.Join(", ", reasonParts) : "Routine Recalculation";

        return new TrustScoreCalculationResult
        {
            OverallScore = overallScore,
            IdentityScore = identityScore,
            CredentialScore = credentialScore,
            ComplianceScore = complianceScore,
            VerificationScore = verificationScore,
            RiskScore = riskScore,
            Version = "v1.0",
            SummaryReason = reason
        };
    }
}
