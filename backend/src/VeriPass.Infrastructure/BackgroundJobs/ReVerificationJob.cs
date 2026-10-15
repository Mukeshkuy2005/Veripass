using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class ReVerificationJob
{
    private readonly IApplicationDbContext _context;
    private readonly IVerificationProviderFactory _providerFactory;
    private readonly ILogger<ReVerificationJob> _logger;

    public ReVerificationJob(
        IApplicationDbContext context,
        IVerificationProviderFactory providerFactory,
        ILogger<ReVerificationJob> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Periodic Re-Verification Job...");

        // Find verified credentials whose last verification was more than 90 days ago
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var credentialsDue = await _context.Credentials
            .Include(c => c.CredentialType)
            .Include(c => c.Business)
            .Where(c => c.VerificationStatus == VerificationStatus.VERIFIED &&
                        c.LastVerifiedAt != null &&
                        c.LastVerifiedAt <= cutoff)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var cred in credentialsDue)
        {
            try
            {
                var provider = _providerFactory.GetProvider(cred.CredentialType.Category);
                if (provider != null)
                {
                    var result = await provider.VerifyAsync(
                        cred.CredentialDisplayValue ?? string.Empty,
                        new Dictionary<string, string> { ["LegalName"] = cred.Business.LegalName },
                        cancellationToken);

                    if (!result.IsValid)
                    {
                        cred.VerificationStatus = VerificationStatus.FLAGGED;
                        
                        var riskSignal = new RiskSignal
                        {
                            BusinessId = cred.BusinessId,
                            SignalType = "STATUTORY_STATUS_CHANGED",
                            Severity = RiskLevel.HIGH,
                            Description = $"Periodic re-verification failed for {cred.CredentialType.Name}: {result.ErrorMessage ?? "Registry mismatch"}",
                            Status = "OPEN",
                            DetectedAt = DateTime.UtcNow
                        };
                        _context.RiskSignals.Add(riskSignal);
                    }
                    else
                    {
                        cred.LastVerifiedAt = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed periodic reverification for credential {CredentialId}", cred.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Re-Verification Job evaluated {Count} credentials.", credentialsDue.Count);
    }
}
