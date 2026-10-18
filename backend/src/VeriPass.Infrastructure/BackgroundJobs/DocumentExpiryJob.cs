using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class DocumentExpiryJob
{
    private readonly IApplicationDbContext _context;
    private readonly IBuyerTrustScoreService _buyerTrustScoreService;
    private readonly ILogger<DocumentExpiryJob> _logger;

    public DocumentExpiryJob(
        IApplicationDbContext context,
        IBuyerTrustScoreService buyerTrustScoreService,
        ILogger<DocumentExpiryJob> logger)
    {
        _context = context;
        _buyerTrustScoreService = buyerTrustScoreService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Document Expiry Monitoring Job...");

        var now = DateTime.UtcNow;
        var thresholdDate = now.AddDays(30);
        var affectedSupplierIds = new HashSet<Guid>();

        var expiringCredentials = await _context.Credentials
            .Include(c => c.CredentialType)
            .Include(c => c.Business)
            .Where(c => c.ExpiryDate != null && c.ExpiryDate <= thresholdDate && c.Status != CredentialStatus.EXPIRED)
            .ToListAsync(cancellationToken);

        foreach (var cred in expiringCredentials)
        {
            var daysRemaining = (cred.ExpiryDate!.Value - now).TotalDays;

            if (daysRemaining <= 0)
            {
                cred.Status = CredentialStatus.EXPIRED;
                cred.VerificationStatus = VerificationStatus.EXPIRED;
                affectedSupplierIds.Add(cred.BusinessId);

                var expiredEvent = new ComplianceEvent
                {
                    BusinessId = cred.BusinessId,
                    CredentialId = cred.Id,
                    EventType = "DOCUMENT_EXPIRED",
                    Severity = ComplianceSeverity.CRITICAL,
                    Title = $"{cred.CredentialType.Name} Expired",
                    Description = $"Credential expired on {cred.ExpiryDate:yyyy-MM-dd}. Verification status updated to EXPIRED.",
                    DueDate = now
                };
                _context.ComplianceEvents.Add(expiredEvent);
            }
            else
            {
                var existingAlert = await _context.ComplianceEvents
                    .AnyAsync(e => e.CredentialId == cred.Id && e.EventType == "DOCUMENT_EXPIRING" && e.ResolvedAt == null, cancellationToken);

                if (!existingAlert)
                {
                    var warningEvent = new ComplianceEvent
                    {
                        BusinessId = cred.BusinessId,
                        CredentialId = cred.Id,
                        EventType = "DOCUMENT_EXPIRING",
                        Severity = daysRemaining <= 7 ? ComplianceSeverity.HIGH : ComplianceSeverity.MEDIUM,
                        Title = $"{cred.CredentialType.Name} Expiring Soon",
                        Description = $"Credential will expire in {(int)daysRemaining} days on {cred.ExpiryDate:yyyy-MM-dd}. Please upload updated renewal.",
                        DueDate = cred.ExpiryDate
                    };
                    _context.ComplianceEvents.Add(warningEvent);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Document Expiry Job processed {Count} expiring credentials. Recalculating for {AffectedCount} suppliers.", expiringCredentials.Count, affectedSupplierIds.Count);

        // Recalculate affected buyer-supplier pairs
        foreach (var supplierId in affectedSupplierIds)
        {
            try
            {
                await _buyerTrustScoreService.RecalculateAllForSupplierAsync(supplierId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate trust score for expired supplier {SupplierId}", supplierId);
            }
        }
    }
}
