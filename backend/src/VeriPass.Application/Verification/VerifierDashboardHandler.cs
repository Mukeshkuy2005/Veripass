using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Verification;

// --- DTOs ---

public record VerifierDashboardSummaryDto(
    int Pending,
    int PendingTodayDelta,
    int InReview,
    int AssignedToCurrentVerifier,
    int Approved,
    int ApprovedTodayDelta,
    int Rejected,
    int Escalated);

public record VerifierPriorityCaseDto(
    Guid Id,
    string Priority,
    string BusinessName,
    string VerificationType,
    DateTime SubmittedAt,
    string RiskLevel,
    string Status,
    string? CaseNumber);

public record VerifierPerformanceDto(
    int CasesReviewedToday,
    int AverageReviewTimeSeconds,
    double ApprovalRate,
    double EscalationRate);

public record VerifierRiskDistributionDto(
    double Low,
    double Medium,
    double High);

public record VerifierRecentDecisionDto(
    Guid Id,
    string VerificationType,
    string Decision,
    string BusinessName,
    DateTime Timestamp,
    string VerifierName);

public record VerifierDashboardResponseDto(
    VerifierDashboardSummaryDto Summary,
    List<VerifierPriorityCaseDto> PriorityCases,
    VerifierPerformanceDto Performance,
    VerifierRiskDistributionDto RiskDistribution,
    List<VerifierRecentDecisionDto> RecentDecisions);

// --- Query & Handler ---

public record GetVerifierDashboardQuery : IRequest<Result<VerifierDashboardResponseDto>>;

public class GetVerifierDashboardQueryHandler : IRequestHandler<GetVerifierDashboardQuery, Result<VerifierDashboardResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVerifierDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<VerifierDashboardResponseDto>> Handle(
        GetVerifierDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var startOfToday = DateTime.UtcNow.Date;

        var baseQuery = _context.VerificationCases
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        // 1. Operational KPI Summary
        var pending = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.PENDING &&
                             c.FinalDecision != VerificationDecision.APPROVED &&
                             c.FinalDecision != VerificationDecision.REJECTED &&
                             c.FinalDecision != VerificationDecision.ESCALATED, cancellationToken);

        var pendingTodayDelta = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.PENDING &&
                             c.FinalDecision != VerificationDecision.APPROVED &&
                             c.FinalDecision != VerificationDecision.REJECTED &&
                             c.FinalDecision != VerificationDecision.ESCALATED &&
                             c.CreatedAt >= startOfToday, cancellationToken);

        var inReview = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.IN_PROGRESS &&
                             c.FinalDecision != VerificationDecision.APPROVED &&
                             c.FinalDecision != VerificationDecision.REJECTED &&
                             c.FinalDecision != VerificationDecision.ESCALATED, cancellationToken);

        var assignedToCurrentVerifier = currentUserId.HasValue
            ? await baseQuery.CountAsync(c => c.Status == VerificationStatus.IN_PROGRESS &&
                                              c.FinalDecision != VerificationDecision.APPROVED &&
                                              c.FinalDecision != VerificationDecision.REJECTED &&
                                              c.FinalDecision != VerificationDecision.ESCALATED &&
                                              c.AssignedVerifierId == currentUserId.Value, cancellationToken)
            : 0;

        var approved = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.VERIFIED || c.FinalDecision == VerificationDecision.APPROVED, cancellationToken);

        var approvedTodayDelta = await baseQuery
            .CountAsync(c => (c.Status == VerificationStatus.VERIFIED || c.FinalDecision == VerificationDecision.APPROVED) && c.CompletedAt >= startOfToday, cancellationToken);

        var rejected = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.REJECTED || c.FinalDecision == VerificationDecision.REJECTED, cancellationToken);

        var escalated = await baseQuery
            .CountAsync(c => c.FinalDecision == VerificationDecision.ESCALATED || c.Status == VerificationStatus.FLAGGED, cancellationToken);

        var summary = new VerifierDashboardSummaryDto(
            pending,
            pendingTodayDelta,
            inReview,
            assignedToCurrentVerifier,
            approved,
            approvedTodayDelta,
            rejected,
            escalated);

        // 2. Priority Cases Queue (Sorted by Escalation > High Risk/Urgency > Older Submission)
        var activeCasesRaw = await baseQuery
            .Include(c => c.Business)
            .Where(c => c.Status == VerificationStatus.PENDING ||
                        c.Status == VerificationStatus.IN_PROGRESS ||
                        c.Status == VerificationStatus.FLAGGED ||
                        c.FinalDecision == VerificationDecision.ESCALATED)
            .ToListAsync(cancellationToken);

        var priorityCases = activeCasesRaw
            .OrderBy(c => c.FinalDecision == VerificationDecision.ESCALATED || c.Status == VerificationStatus.FLAGGED ? 0 :
                          c.RiskLevel == RiskLevel.HIGH || c.RiskLevel == RiskLevel.CRITICAL || c.Priority == "HIGH" || c.Priority == "URGENT" ? 1 :
                          c.Priority == "MEDIUM" || c.RiskLevel == RiskLevel.MEDIUM ? 2 : 3)
            .ThenBy(c => c.CreatedAt)
            .Take(5)
            .Select(c => new VerifierPriorityCaseDto(
                c.Id,
                NormalizePriority(c.Priority, c.RiskLevel, c.FinalDecision),
                c.Business?.LegalName ?? "Unknown Business",
                c.VerificationType,
                c.CreatedAt,
                NormalizeRisk(c.RiskLevel),
                c.Status.ToString(),
                $"VP-{c.Id.ToString("N")[..5].ToUpperInvariant()}"))
            .ToList();

        // 3. Operational Performance (Contextual operational benchmarks)
        var reviewedTodayCount = await baseQuery
            .CountAsync(c => c.CompletedAt >= startOfToday &&
                             (c.Status == VerificationStatus.VERIFIED || c.Status == VerificationStatus.REJECTED ||
                              c.FinalDecision == VerificationDecision.APPROVED || c.FinalDecision == VerificationDecision.REJECTED),
                        cancellationToken);

        // Average Review Time: based on completed cases with valid timestamps
        var durationCasesQuery = baseQuery
            .Where(c => c.StartedAt != null && c.CompletedAt != null && c.CompletedAt > c.StartedAt);

        var todayDurations = await durationCasesQuery
            .Where(c => c.CompletedAt >= startOfToday)
            .Select(c => new { c.StartedAt, c.CompletedAt })
            .ToListAsync(cancellationToken);

        int avgReviewTimeSeconds = 0;
        if (todayDurations.Any())
        {
            avgReviewTimeSeconds = (int)todayDurations
                .Average(d => (d.CompletedAt!.Value - d.StartedAt!.Value).TotalSeconds);
        }
        else
        {
            var allDurations = await durationCasesQuery
                .Select(c => new { c.StartedAt, c.CompletedAt })
                .Take(50)
                .ToListAsync(cancellationToken);

            if (allDurations.Any())
            {
                avgReviewTimeSeconds = (int)allDurations
                    .Average(d => (d.CompletedAt!.Value - d.StartedAt!.Value).TotalSeconds);
            }
        }

        // Approval Rate: Approved / (Approved + Rejected)
        var approvedDecidedToday = await baseQuery
            .CountAsync(c => c.CompletedAt >= startOfToday &&
                             (c.Status == VerificationStatus.VERIFIED || c.FinalDecision == VerificationDecision.APPROVED), cancellationToken);

        var rejectedDecidedToday = await baseQuery
            .CountAsync(c => c.CompletedAt >= startOfToday &&
                             (c.Status == VerificationStatus.REJECTED || c.FinalDecision == VerificationDecision.REJECTED), cancellationToken);

        double approvalRate = 0.0;
        var totalDecidedToday = approvedDecidedToday + rejectedDecidedToday;
        if (totalDecidedToday > 0)
        {
            approvalRate = Math.Round(((double)approvedDecidedToday / totalDecidedToday) * 100.0, 1);
        }
        else if (approved + rejected > 0)
        {
            // Fallback to overall decided rate if none decided today
            approvalRate = Math.Round(((double)approved / (approved + rejected)) * 100.0, 1);
        }

        // Escalation Rate: Escalated / Total Reviewed
        double escalationRate = 0.0;
        var totalReviewedOverall = approved + rejected + escalated;
        if (totalReviewedOverall > 0)
        {
            escalationRate = Math.Round(((double)escalated / totalReviewedOverall) * 100.0, 1);
        }

        var performance = new VerifierPerformanceDto(
            reviewedTodayCount,
            avgReviewTimeSeconds,
            approvalRate,
            escalationRate);

        // 4. Risk Distribution
        // Calculate distribution over active cases or all non-deleted cases if none active
        var riskPool = activeCasesRaw.Any() ? activeCasesRaw : await baseQuery.ToListAsync(cancellationToken);
        double lowPct = 0, medPct = 0, highPct = 0;
        if (riskPool.Any())
        {
            var totalCount = (double)riskPool.Count;
            var lowCount = riskPool.Count(c => c.RiskLevel == RiskLevel.LOW);
            var medCount = riskPool.Count(c => c.RiskLevel == RiskLevel.MEDIUM);
            var highCount = riskPool.Count(c => c.RiskLevel == RiskLevel.HIGH || c.RiskLevel == RiskLevel.CRITICAL);

            lowPct = Math.Round((lowCount / totalCount) * 100.0, 1);
            medPct = Math.Round((medCount / totalCount) * 100.0, 1);
            highPct = Math.Round((highCount / totalCount) * 100.0, 1);
        }

        var riskDistribution = new VerifierRiskDistributionDto(lowPct, medPct, highPct);

        // 5. Recent Decisions
        var recentDecisionsEntities = await baseQuery
            .Include(c => c.Business)
            .Include(c => c.AssignedVerifier)
            .Where(c => c.CompletedAt != null && c.FinalDecision != VerificationDecision.NONE)
            .OrderByDescending(c => c.CompletedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentDecisions = recentDecisionsEntities.Select(c =>
        {
            var decisionStr = c.FinalDecision switch
            {
                VerificationDecision.APPROVED => "APPROVED",
                VerificationDecision.REJECTED => "REJECTED",
                VerificationDecision.ESCALATED => "ESCALATED",
                VerificationDecision.REQUEST_MORE_INFO => "MORE_INFO",
                _ => c.Status.ToString()
            };

            var verifierName = c.AssignedVerifier != null
                ? $"{c.AssignedVerifier.FirstName} {c.AssignedVerifier.LastName}".Trim()
                : "Compliance Officer";

            return new VerifierRecentDecisionDto(
                c.Id,
                c.VerificationType,
                decisionStr,
                c.Business?.LegalName ?? "Unknown Entity",
                c.CompletedAt!.Value,
                verifierName);
        }).ToList();

        // If no completed cases have decisions yet, check if there are escalated cases or audit logs
        if (!recentDecisions.Any())
        {
            var escalatedOnly = await baseQuery
                .Include(c => c.Business)
                .Include(c => c.AssignedVerifier)
                .Where(c => c.FinalDecision == VerificationDecision.ESCALATED || c.Status == VerificationStatus.FLAGGED)
                .OrderByDescending(c => c.EscalatedAt ?? c.UpdatedAt ?? c.CreatedAt)
                .Take(3)
                .ToListAsync(cancellationToken);

            foreach (var esc in escalatedOnly)
            {
                recentDecisions.Add(new VerifierRecentDecisionDto(
                    esc.Id,
                    esc.VerificationType,
                    "ESCALATED",
                    esc.Business?.LegalName ?? "Unknown Entity",
                    esc.EscalatedAt ?? esc.UpdatedAt ?? esc.CreatedAt,
                    esc.AssignedVerifier != null ? $"{esc.AssignedVerifier.FirstName} {esc.AssignedVerifier.LastName}".Trim() : "Compliance Officer"));
            }
        }

        var response = new VerifierDashboardResponseDto(
            summary,
            priorityCases,
            performance,
            riskDistribution,
            recentDecisions);

        return Result<VerifierDashboardResponseDto>.Success(response);
    }

    private static string NormalizePriority(string priority, RiskLevel riskLevel, VerificationDecision decision)
    {
        if (decision == VerificationDecision.ESCALATED) return "HIGH";
        if (riskLevel == RiskLevel.HIGH || riskLevel == RiskLevel.CRITICAL) return "HIGH";
        if (string.Equals(priority, "HIGH", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(priority, "URGENT", StringComparison.OrdinalIgnoreCase)) return "HIGH";
        if (string.Equals(priority, "MEDIUM", StringComparison.OrdinalIgnoreCase) ||
            riskLevel == RiskLevel.MEDIUM) return "MEDIUM";
        return "LOW";
    }

    private static string NormalizeRisk(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.HIGH or RiskLevel.CRITICAL => "HIGH",
        RiskLevel.MEDIUM => "MEDIUM",
        _ => "LOW"
    };
}
