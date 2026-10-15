using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class ComplianceMonitoringJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ComplianceMonitoringJob> _logger;

    public ComplianceMonitoringJob(IApplicationDbContext context, ILogger<ComplianceMonitoringJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Continuous Compliance Monitoring Job...");

        // Scan businesses with unacknowledged critical or high severity events
        var overdueEvents = await _context.ComplianceEvents
            .Where(e => e.ResolvedAt == null && e.DueDate != null && e.DueDate < DateTime.UtcNow)
            .Include(e => e.Business)
            .ToListAsync(cancellationToken);

        foreach (var evt in overdueEvents)
        {
            if (evt.Severity != ComplianceSeverity.CRITICAL)
            {
                // Escalate severity if overdue
                evt.Severity = ComplianceSeverity.CRITICAL;
                evt.Description += " [ESCALATED: Resolution overdue]";

                // Add risk signal if not already present
                var hasRisk = await _context.RiskSignals
                    .AnyAsync(r => r.BusinessId == evt.BusinessId && r.SignalType == "OVERDUE_COMPLIANCE_DEFAULT", cancellationToken);

                if (!hasRisk)
                {
                    var riskSignal = new RiskSignal
                    {
                        BusinessId = evt.BusinessId,
                        SignalType = "OVERDUE_COMPLIANCE_DEFAULT",
                        Severity = RiskLevel.HIGH,
                        Description = $"Overdue compliance default on event: {evt.Title}",
                        Status = "OPEN",
                        DetectedAt = DateTime.UtcNow
                    };
                    _context.RiskSignals.Add(riskSignal);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Compliance Monitoring Job evaluated {Count} overdue events.", overdueEvents.Count);
    }
}
