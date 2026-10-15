using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class RiskDetectionJob
{
    private readonly IApplicationDbContext _context;
    private readonly ITrustScoreCalculator _trustScoreCalculator;
    private readonly ILogger<RiskDetectionJob> _logger;

    public RiskDetectionJob(
        IApplicationDbContext context,
        ITrustScoreCalculator trustScoreCalculator,
        ILogger<RiskDetectionJob> logger)
    {
        _context = context;
        _trustScoreCalculator = trustScoreCalculator;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Continuous Risk Detection Job...");

        try
        {
            var businesses = await _context.Businesses
                .Include(b => b.RiskSignals)
                .Where(b => b.VerificationStatus != VerificationStatus.REJECTED)
                .ToListAsync(cancellationToken);

            int newRiskSignalsCreated = 0;

            foreach (var business in businesses)
            {
                bool riskProfileChanged = false;

                // 1. Check GSTIN vs PAN statutory alignment (PAN characters must match GSTIN characters 3-12)
                if (!string.IsNullOrWhiteSpace(business.Gstin) && !string.IsNullOrWhiteSpace(business.Pan))
                {
                    if (business.Gstin.Length >= 12)
                    {
                        var panFromGst = business.Gstin.Substring(2, 10);
                        if (!string.Equals(panFromGst, business.Pan, StringComparison.OrdinalIgnoreCase))
                        {
                            var existingSignal = business.RiskSignals.FirstOrDefault(r => r.SignalType == "TAX_IDENTIFIER_MISMATCH" && r.Status == "ACTIVE");
                            if (existingSignal == null)
                            {
                                var signal = new RiskSignal
                                {
                                    BusinessId = business.Id,
                                    SignalType = "TAX_IDENTIFIER_MISMATCH",
                                    Severity = RiskLevel.HIGH,
                                    ScoreImpact = -25,
                                    Description = $"Discrepancy detected: PAN in GSTIN ({panFromGst}) does not match registered PAN ({business.Pan}).",
                                    Status = "ACTIVE",
                                    DetectedAt = DateTime.UtcNow
                                };
                                _context.RiskSignals.Add(signal);
                                newRiskSignalsCreated++;
                                riskProfileChanged = true;
                            }
                        }
                    }
                }

                // 2. Check for unresolved high dispute counts
                var activeDisputesCount = await _context.Disputes
                    .CountAsync(d => d.BusinessId == business.Id && d.Status == DisputeStatus.OPEN, cancellationToken);

                if (activeDisputesCount >= 3)
                {
                    var existingSignal = business.RiskSignals.FirstOrDefault(r => r.SignalType == "HIGH_ACTIVE_DISPUTES" && r.Status == "ACTIVE");
                    if (existingSignal == null)
                    {
                        var signal = new RiskSignal
                        {
                            BusinessId = business.Id,
                            SignalType = "HIGH_ACTIVE_DISPUTES",
                            Severity = RiskLevel.MEDIUM,
                            ScoreImpact = -15,
                            Description = $"Business has {activeDisputesCount} unresolved open commercial disputes.",
                            Status = "ACTIVE",
                            DetectedAt = DateTime.UtcNow
                        };
                        _context.RiskSignals.Add(signal);
                        newRiskSignalsCreated++;
                        riskProfileChanged = true;
                    }
                }

                // 3. Recalculate Trust Score if any risk profile changed
                if (riskProfileChanged)
                {
                    var calculation = await _trustScoreCalculator.CalculateAsync(business.Id, cancellationToken);
                    var currentTrust = await _context.TrustScores.FirstOrDefaultAsync(t => t.BusinessId == business.Id, cancellationToken);
                    if (currentTrust != null)
                    {
                        currentTrust.OverallScore = calculation.OverallScore;
                        currentTrust.RiskScore = calculation.RiskScore;
                        currentTrust.LastCalculatedAt = DateTime.UtcNow;
                    }

                    _context.TrustScoreSnapshots.Add(new TrustScoreSnapshot
                    {
                        BusinessId = business.Id,
                        OverallScore = calculation.OverallScore,
                        IdentityScore = calculation.IdentityScore,
                        CredentialScore = calculation.CredentialScore,
                        ComplianceScore = calculation.ComplianceScore,
                        VerificationScore = calculation.VerificationScore,
                        RiskScore = calculation.RiskScore,
                        CalculationVersion = calculation.Version,
                        Reason = "Automated Risk Detection Recalibration"
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Risk Detection Job finished. Created {Count} new risk signal(s).", newRiskSignalsCreated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while executing Risk Detection Job.");
        }
    }
}
