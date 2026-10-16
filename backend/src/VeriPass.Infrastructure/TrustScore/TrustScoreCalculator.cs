using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
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

        var cases = await _context.VerificationCases
            .Where(c => c.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        // Gather all distinct verified statutory types (Option B: Each unique type contributes once)
        var verifiedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. From verified credentials
        foreach (var cred in business.Credentials.Where(c => c.VerificationStatus == VerificationStatus.VERIFIED))
        {
            var type = DocumentScoringPolicy.NormalizeType(cred.CredentialType?.Code ?? cred.CredentialType?.Name);
            verifiedTypes.Add(type);
        }

        // 2. From approved documents
        foreach (var doc in business.Documents.Where(d => DocumentScoringPolicy.IsDocumentApproved(d.Status)))
        {
            var type = DocumentScoringPolicy.NormalizeType(doc.DocumentType);
            verifiedTypes.Add(type);
        }

        // 3. From approved verification cases
        foreach (var vc in cases.Where(c => DocumentScoringPolicy.IsVerificationApproved(c.FinalDecision, c.Status)))
        {
            var type = DocumentScoringPolicy.NormalizeType(vc.VerificationType);
            verifiedTypes.Add(type);
        }

        // 1. Identity Score (25% Weight)
        // Evaluates presence and validity of PAN, GSTIN, CIN/Udyam, Directors, Registered Address
        int identityPoints = 0;
        bool hasPan = !string.IsNullOrWhiteSpace(business.Pan) || verifiedTypes.Contains("PAN");
        bool hasGstin = !string.IsNullOrWhiteSpace(business.Gstin) || verifiedTypes.Contains("GST");
        bool hasCinOrUdyam = !string.IsNullOrWhiteSpace(business.Cin) || !string.IsNullOrWhiteSpace(business.UdyamNumber) || verifiedTypes.Contains("MSME");
        bool hasDirectors = business.Directors.Count > 0;
        bool hasAddress = business.Addresses.Any(a => a.IsPrimary) || verifiedTypes.Contains("ADDRESS");

        if (hasPan) identityPoints += 25;
        if (hasGstin) identityPoints += 25;
        if (hasCinOrUdyam) identityPoints += 20;
        if (hasDirectors) identityPoints += 15;
        if (hasAddress) identityPoints += 15;
        int identityScore = Math.Clamp(identityPoints, 0, 100);

        // 2. Credential Score (25% Weight)
        // Evaluates completeness and active status of required statutory credentials
        int credentialScore = 0;
        if (business.Credentials.Count > 0)
        {
            var verifiedCount = business.Credentials.Count(c => c.VerificationStatus == VerificationStatus.VERIFIED);
            var activeCount = business.Credentials.Count(c => c.Status != CredentialStatus.EXPIRED && c.Status != CredentialStatus.SUSPENDED);
            var extraVerifiedTypes = verifiedTypes.Count(t => !business.Credentials.Any(c => DocumentScoringPolicy.NormalizeType(c.CredentialType?.Code ?? c.CredentialType?.Name) == t));
            var totalTypes = business.Credentials.Count + extraVerifiedTypes;
            var ratio = (double)(verifiedCount + activeCount + (extraVerifiedTypes * 2)) / (totalTypes * 2);
            credentialScore = Math.Clamp((int)(ratio * 100), 0, 100);
        }
        else
        {
            var submittedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in business.Documents)
            {
                submittedTypes.Add(DocumentScoringPolicy.NormalizeType(doc.DocumentType));
            }
            foreach (var vc in cases)
            {
                submittedTypes.Add(DocumentScoringPolicy.NormalizeType(vc.VerificationType));
            }
            int totalRequired = Math.Max(DocumentScoringPolicy.BaselineRequiredStatutoryCount, submittedTypes.Count);
            credentialScore = totalRequired > 0 
                ? Math.Clamp((int)((double)verifiedTypes.Count / totalRequired * 100), 0, 100)
                : 0;
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
        // Checks completed verification cases and confidence scores (each distinct statutory verification factor adds 25 points up to 100)
        int verificationScore = Math.Clamp(verifiedTypes.Count * DocumentScoringPolicy.PointsPerVerifiedStatutoryType, 0, DocumentScoringPolicy.MaxVerificationScore);

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
        if (credentialScore >= 80 || verifiedTypes.Count >= 2) reasonParts.Add("Verified Credentials");
        if (verifiedTypes.Count > 0) reasonParts.Add($"{verifiedTypes.Count} Statutory Factor(s) Verified");
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
