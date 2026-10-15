using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class TrustScoreRecalculationJob
{
    private readonly IApplicationDbContext _context;
    private readonly ITrustScoreCalculator _trustCalculator;
    private readonly ILogger<TrustScoreRecalculationJob> _logger;

    public TrustScoreRecalculationJob(
        IApplicationDbContext context,
        ITrustScoreCalculator trustCalculator,
        ILogger<TrustScoreRecalculationJob> logger)
    {
        _context = context;
        _trustCalculator = trustCalculator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Scheduled Trust Score Recalculation Job...");

        // Identify businesses needing recalculation (e.g. not recalculated in 7 days or recently modified)
        var staleDate = DateTime.UtcNow.AddDays(-7);
        var staleBusinessIds = await _context.Businesses
            .Where(b => b.TrustScore == null || b.TrustScore.LastCalculatedAt <= staleDate)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var businessId in staleBusinessIds)
        {
            try
            {
                var calc = await _trustCalculator.CalculateAsync(businessId, cancellationToken);
                var existingTrustScore = await _context.TrustScores
                    .FirstOrDefaultAsync(t => t.BusinessId == businessId, cancellationToken);

                if (existingTrustScore != null)
                {
                    existingTrustScore.OverallScore = calc.OverallScore;
                    existingTrustScore.IdentityScore = calc.IdentityScore;
                    existingTrustScore.CredentialScore = calc.CredentialScore;
                    existingTrustScore.ComplianceScore = calc.ComplianceScore;
                    existingTrustScore.VerificationScore = calc.VerificationScore;
                    existingTrustScore.RiskScore = calc.RiskScore;
                    existingTrustScore.LastCalculatedAt = DateTime.UtcNow;
                }
                else
                {
                    var newScore = new TrustScore
                    {
                        BusinessId = businessId,
                        OverallScore = calc.OverallScore,
                        IdentityScore = calc.IdentityScore,
                        CredentialScore = calc.CredentialScore,
                        ComplianceScore = calc.ComplianceScore,
                        VerificationScore = calc.VerificationScore,
                        RiskScore = calc.RiskScore,
                        CalculationVersion = calc.Version,
                        LastCalculatedAt = DateTime.UtcNow
                    };
                    _context.TrustScores.Add(newScore);
                }

                var snapshot = new TrustScoreSnapshot
                {
                    BusinessId = businessId,
                    OverallScore = calc.OverallScore,
                    IdentityScore = calc.IdentityScore,
                    CredentialScore = calc.CredentialScore,
                    ComplianceScore = calc.ComplianceScore,
                    VerificationScore = calc.VerificationScore,
                    RiskScore = calc.RiskScore,
                    CalculationVersion = calc.Version,
                    Reason = $"Periodic Automated Recalculation: {calc.SummaryReason}"
                };
                _context.TrustScoreSnapshots.Add(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate trust score for Business {BusinessId}", businessId);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Trust Score Recalculation Job updated {Count} businesses.", staleBusinessIds.Count);
    }
}
