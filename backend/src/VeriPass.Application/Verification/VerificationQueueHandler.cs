using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Verification;

// --- DTOs ---

public record VerificationQueueAssigneeDto(
    Guid Id,
    string Name);

public record VerificationQueueSlaDto(
    DateTime DueAt,
    bool IsOverdue,
    int RemainingMinutes,
    string FormattedRemaining);

public record VerificationQueueItemDto(
    Guid Id,
    string CaseNumber,
    Guid BusinessId,
    string BusinessName,
    string? BusinessIdentifier,
    string VerificationType,
    DateTime SubmittedAt,
    string RiskLevel,
    int RiskScore,
    string Priority,
    VerificationQueueAssigneeDto? AssignedTo,
    VerificationQueueSlaDto Sla,
    string Status,
    string ActionLabel,
    int? TrustScore = null);

public record VerificationQueueSummaryDto(
    int TotalWaiting,
    int MyCasesCount,
    int InReviewCount,
    int ActionableCount);

public record VerificationQueueResponseDto(
    List<VerificationQueueItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    VerificationQueueSummaryDto Summary);

// --- Queries ---

public record GetVerificationQueueQuery(
    string? Scope = "all", // "my" or "all"
    string? Search = null,
    string? Status = null, // "ALL", "PENDING", "IN_REVIEW", "MORE_INFO_REQUIRED", "APPROVED", "REJECTED", "ESCALATED", "CANCELLED"
    string? Priority = null, // "ALL", "HIGH", "MEDIUM", "LOW"
    string? Type = null, // "ALL", "GST", "PAN", "Address", "Bank", etc.
    string? Risk = null, // "ALL", "LOW", "MEDIUM", "HIGH"
    string? DateRange = null, // "today", "24h", "7d", "30d", "custom"
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = "submitted", // "submitted", "priority", "risk", "sla", "business"
    string? SortDirection = "desc" // "asc", "desc"
) : IRequest<Result<VerificationQueueResponseDto>>;

public record GetVerificationQueueSummaryQuery : IRequest<Result<VerificationQueueSummaryDto>>;

// --- Handler ---

public class VerificationQueueHandler :
    IRequestHandler<GetVerificationQueueQuery, Result<VerificationQueueResponseDto>>,
    IRequestHandler<GetVerificationQueueSummaryQuery, Result<VerificationQueueSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public VerificationQueueHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<VerificationQueueResponseDto>> Handle(
        GetVerificationQueueQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        var baseQuery = _context.VerificationCases
            .AsNoTracking()
            .Include(c => c.Business)
                .ThenInclude(b => b.TrustScore)
            .Include(c => c.Business)
                .ThenInclude(b => b.Passport)
            .Include(c => c.AssignedVerifier)
            .Where(c => !c.IsDeleted);

        // 1. Calculate Summary Counts (authoritative across active cases)
        var waitingCount = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.PENDING &&
                             c.FinalDecision != VerificationDecision.APPROVED &&
                             c.FinalDecision != VerificationDecision.REJECTED &&
                             c.FinalDecision != VerificationDecision.ESCALATED &&
                             c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO, cancellationToken);

        var myCount = 0;
        if (currentUserId.HasValue)
        {
            myCount = await baseQuery
                .CountAsync(c => c.AssignedVerifierId == currentUserId.Value &&
                                 (c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS), cancellationToken);
        }

        var inReviewCount = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.IN_PROGRESS &&
                             c.FinalDecision != VerificationDecision.ESCALATED &&
                             c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO, cancellationToken);

        // 2. Apply Scope Filtering
        var filteredQuery = baseQuery;
        var isMyScope = string.Equals(request.Scope, "my", StringComparison.OrdinalIgnoreCase);

        if (isMyScope)
        {
            if (!currentUserId.HasValue)
            {
                // Not authenticated or no verifier ID claim
                return Result<VerificationQueueResponseDto>.Success(new VerificationQueueResponseDto(
                    new List<VerificationQueueItemDto>(),
                    request.Page,
                    request.PageSize,
                    0,
                    0,
                    new VerificationQueueSummaryDto(waitingCount, 0, inReviewCount, 0)));
            }

            filteredQuery = filteredQuery.Where(c => c.AssignedVerifierId == currentUserId.Value);
        }

        // 3. Apply Search Filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            filteredQuery = filteredQuery.Where(c =>
                c.Business.LegalName.ToLower().Contains(search) ||
                (c.Business.Gstin != null && c.Business.Gstin.ToLower().Contains(search)) ||
                (c.Business.Pan != null && c.Business.Pan.ToLower().Contains(search)) ||
                (c.Business.BusinessIdNumber != null && c.Business.BusinessIdNumber.ToLower().Contains(search)) ||
                c.Id.ToString().ToLower().Contains(search) ||
                (c.DecisionReason != null && c.DecisionReason.ToLower().Contains(search)));
        }

        // 4. Apply Status Filter
        if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var statusNormalized = request.Status.Trim().ToUpperInvariant();
            switch (statusNormalized)
            {
                case "PENDING":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.PENDING &&
                        c.FinalDecision != VerificationDecision.APPROVED &&
                        c.FinalDecision != VerificationDecision.REJECTED &&
                        c.FinalDecision != VerificationDecision.ESCALATED &&
                        c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO);
                    break;

                case "IN_REVIEW":
                case "IN_PROGRESS":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.IN_PROGRESS &&
                        c.FinalDecision != VerificationDecision.ESCALATED &&
                        c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO);
                    break;

                case "MORE_INFO_REQUIRED":
                case "REQUEST_MORE_INFO":
                    filteredQuery = filteredQuery.Where(c =>
                        c.FinalDecision == VerificationDecision.REQUEST_MORE_INFO);
                    break;

                case "APPROVED":
                case "VERIFIED":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.VERIFIED ||
                        c.FinalDecision == VerificationDecision.APPROVED);
                    break;

                case "REJECTED":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.REJECTED ||
                        c.FinalDecision == VerificationDecision.REJECTED);
                    break;

                case "ESCALATED":
                case "FLAGGED":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.FLAGGED ||
                        c.FinalDecision == VerificationDecision.ESCALATED);
                    break;

                case "CANCELLED":
                case "EXPIRED":
                    filteredQuery = filteredQuery.Where(c =>
                        c.Status == VerificationStatus.EXPIRED);
                    break;
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Status))
        {
            // Default actionable queue when status is not specified: PENDING, IN_PROGRESS, ESCALATED, MORE_INFO_REQUIRED
            filteredQuery = filteredQuery.Where(c =>
                c.Status == VerificationStatus.PENDING ||
                c.Status == VerificationStatus.IN_PROGRESS ||
                c.Status == VerificationStatus.FLAGGED ||
                c.FinalDecision == VerificationDecision.ESCALATED ||
                c.FinalDecision == VerificationDecision.REQUEST_MORE_INFO);
        }

        // 5. Apply Priority Filter
        if (!string.IsNullOrWhiteSpace(request.Priority) && !string.Equals(request.Priority, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var pUpper = request.Priority.Trim().ToUpperInvariant();
            if (pUpper == "HIGH")
            {
                filteredQuery = filteredQuery.Where(c => c.Priority == "HIGH" || c.Priority == "URGENT");
            }
            else if (pUpper == "MEDIUM")
            {
                filteredQuery = filteredQuery.Where(c => c.Priority == "MEDIUM" || c.Priority == "NORMAL");
            }
            else if (pUpper == "LOW")
            {
                filteredQuery = filteredQuery.Where(c => c.Priority == "LOW");
            }
        }

        // 6. Apply Verification Type Filter
        if (!string.IsNullOrWhiteSpace(request.Type) && !string.Equals(request.Type, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var tUpper = request.Type.Trim().ToUpperInvariant();
            filteredQuery = filteredQuery.Where(c => c.VerificationType.ToUpper() == tUpper);
        }

        // 7. Apply Risk Level Filter
        if (!string.IsNullOrWhiteSpace(request.Risk) && !string.Equals(request.Risk, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var rUpper = request.Risk.Trim().ToUpperInvariant();
            if (rUpper == "HIGH")
            {
                filteredQuery = filteredQuery.Where(c => c.RiskLevel == RiskLevel.HIGH || c.RiskLevel == RiskLevel.CRITICAL);
            }
            else if (rUpper == "MEDIUM")
            {
                filteredQuery = filteredQuery.Where(c => c.RiskLevel == RiskLevel.MEDIUM);
            }
            else if (rUpper == "LOW")
            {
                filteredQuery = filteredQuery.Where(c => c.RiskLevel == RiskLevel.LOW);
            }
        }

        // 8. Apply Date Filter (Timezone-aware UTC boundaries)
        if (!string.IsNullOrWhiteSpace(request.DateRange) && !string.Equals(request.DateRange, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var dRange = request.DateRange.Trim().ToLowerInvariant();
            switch (dRange)
            {
                case "today":
                case "24h":
                    var dayAgo = now.AddHours(-24);
                    filteredQuery = filteredQuery.Where(c => c.CreatedAt >= dayAgo);
                    break;
                case "7d":
                    var sevenDaysAgo = now.AddDays(-7);
                    filteredQuery = filteredQuery.Where(c => c.CreatedAt >= sevenDaysAgo);
                    break;
                case "30d":
                    var thirtyDaysAgo = now.AddDays(-30);
                    filteredQuery = filteredQuery.Where(c => c.CreatedAt >= thirtyDaysAgo);
                    break;
                case "custom":
                    if (request.DateFrom.HasValue)
                    {
                        var fromUtc = DateTime.SpecifyKind(request.DateFrom.Value, DateTimeKind.Utc);
                        filteredQuery = filteredQuery.Where(c => c.CreatedAt >= fromUtc);
                    }
                    if (request.DateTo.HasValue)
                    {
                        var toUtc = DateTime.SpecifyKind(request.DateTo.Value, DateTimeKind.Utc);
                        filteredQuery = filteredQuery.Where(c => c.CreatedAt <= toUtc);
                    }
                    break;
            }
        }

        // Total matching count before pagination
        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        // 9. Server-Side Sorting
        var isAsc = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "submitted";

        IOrderedQueryable<VerificationCase> orderedQuery;
        switch (sortBy)
        {
            case "priority":
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.Priority == "LOW" ? 1 : c.Priority == "MEDIUM" || c.Priority == "NORMAL" ? 2 : 3)
                    : filteredQuery.OrderByDescending(c => c.Priority == "URGENT" || c.Priority == "HIGH" ? 3 : c.Priority == "MEDIUM" || c.Priority == "NORMAL" ? 2 : 1);
                break;

            case "risk":
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.RiskLevel)
                    : filteredQuery.OrderByDescending(c => c.RiskLevel);
                break;

            case "business":
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.Business.LegalName)
                    : filteredQuery.OrderByDescending(c => c.Business.LegalName);
                break;

            case "type":
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.VerificationType)
                    : filteredQuery.OrderByDescending(c => c.VerificationType);
                break;

            case "sla":
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.CreatedAt)
                    : filteredQuery.OrderByDescending(c => c.CreatedAt);
                break;

            case "submitted":
            default:
                orderedQuery = isAsc
                    ? filteredQuery.OrderBy(c => c.CreatedAt)
                    : filteredQuery.OrderByDescending(c => c.CreatedAt);
                break;
        }

        // Clamped pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var pagedEntities = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 10. Lightweight DTO Projection with SLA & Status Calculations
        var items = pagedEntities.Select(c =>
        {
            var canonicalStatus = ResolveCanonicalStatus(c);
            var priority = NormalizePriority(c.Priority, c.RiskLevel, c.FinalDecision);
            var risk = NormalizeRisk(c.RiskLevel);
            var authoritativeScore = c.Business?.TrustScore?.OverallScore 
                ?? (c.Business?.Passport != null ? c.Business.Passport.VerificationScore : (int?)null)
                ?? (c.RiskLevel switch
                {
                    RiskLevel.CRITICAL => 15,
                    RiskLevel.HIGH => 25,
                    RiskLevel.MEDIUM => 50,
                    _ => 85
                });
            var riskScore = authoritativeScore;
            var sla = CalculateSla(c, now);
            var action = ResolveActionLabel(canonicalStatus);

            return new VerificationQueueItemDto(
                c.Id,
                $"VP-{c.Id.ToString("N")[..5].ToUpperInvariant()}",
                c.BusinessId,
                c.Business.LegalName,
                c.Business.Gstin ?? c.Business.Pan ?? c.Business.BusinessIdNumber,
                c.VerificationType,
                c.CreatedAt,
                risk,
                riskScore,
                priority,
                c.AssignedVerifier != null
                    ? new VerificationQueueAssigneeDto(c.AssignedVerifier.Id, $"{c.AssignedVerifier.FirstName} {c.AssignedVerifier.LastName}".Trim())
                    : c.AssignedVerifierId.HasValue
                        ? new VerificationQueueAssigneeDto(c.AssignedVerifierId.Value, "Assigned Verifier")
                        : null,
                sla,
                canonicalStatus,
                action,
                authoritativeScore);
        }).ToList();

        var summary = new VerificationQueueSummaryDto(
            waitingCount,
            myCount,
            inReviewCount,
            totalCount);

        return Result<VerificationQueueResponseDto>.Success(new VerificationQueueResponseDto(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            summary));
    }

    public async Task<Result<VerificationQueueSummaryDto>> Handle(
        GetVerificationQueueSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;

        var baseQuery = _context.VerificationCases
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        var waitingCount = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.PENDING &&
                             c.FinalDecision != VerificationDecision.APPROVED &&
                             c.FinalDecision != VerificationDecision.REJECTED &&
                             c.FinalDecision != VerificationDecision.ESCALATED &&
                             c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO, cancellationToken);

        var myCount = 0;
        if (currentUserId.HasValue)
        {
            myCount = await baseQuery
                .CountAsync(c => c.AssignedVerifierId == currentUserId.Value &&
                                 (c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS), cancellationToken);
        }

        var inReviewCount = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.IN_PROGRESS &&
                             c.FinalDecision != VerificationDecision.ESCALATED &&
                             c.FinalDecision != VerificationDecision.REQUEST_MORE_INFO, cancellationToken);

        var actionableCount = await baseQuery
            .CountAsync(c => c.Status == VerificationStatus.PENDING ||
                             c.Status == VerificationStatus.IN_PROGRESS ||
                             c.Status == VerificationStatus.FLAGGED ||
                             c.FinalDecision == VerificationDecision.ESCALATED ||
                             c.FinalDecision == VerificationDecision.REQUEST_MORE_INFO, cancellationToken);

        return Result<VerificationQueueSummaryDto>.Success(new VerificationQueueSummaryDto(
            waitingCount,
            myCount,
            inReviewCount,
            actionableCount));
    }

    // --- Helpers ---

    private static string ResolveCanonicalStatus(VerificationCase c)
    {
        if (c.FinalDecision == VerificationDecision.ESCALATED || c.Status == VerificationStatus.FLAGGED)
            return "ESCALATED";
        if (c.FinalDecision == VerificationDecision.REQUEST_MORE_INFO)
            return "MORE_INFO_REQUIRED";
        if (c.FinalDecision == VerificationDecision.APPROVED || c.Status == VerificationStatus.VERIFIED)
            return "APPROVED";
        if (c.FinalDecision == VerificationDecision.REJECTED || c.Status == VerificationStatus.REJECTED)
            return "REJECTED";
        if (c.Status == VerificationStatus.IN_PROGRESS)
            return "IN_REVIEW";
        if (c.Status == VerificationStatus.EXPIRED)
            return "CANCELLED";

        return "PENDING";
    }

    private static string ResolveActionLabel(string status) => status switch
    {
        "PENDING" => "Review",
        "IN_REVIEW" => "Continue Review",
        "MORE_INFO_REQUIRED" => "Review",
        "ESCALATED" => "View",
        "APPROVED" => "View",
        "REJECTED" => "View",
        "CANCELLED" => "View",
        _ => "Review"
    };

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

    private static int CalculateRiskScore(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.CRITICAL => 15,
        RiskLevel.HIGH => 25,
        RiskLevel.MEDIUM => 50,
        _ => 85
    };

    private static VerificationQueueSlaDto CalculateSla(VerificationCase c, DateTime now)
    {
        // Canonical SLA policy:
        // HIGH/URGENT priority: 4h SLA from submission (or escalation)
        // MEDIUM/NORMAL priority: 24h SLA
        // LOW priority: 48h SLA
        var hours = c.Priority == "HIGH" || c.Priority == "URGENT" || c.FinalDecision == VerificationDecision.ESCALATED ? 4 :
                    c.Priority == "LOW" ? 48 : 24;

        var baseTime = c.EscalatedAt ?? c.CreatedAt;
        var dueAt = baseTime.AddHours(hours);

        var isCompleted = c.Status == VerificationStatus.VERIFIED ||
                          c.Status == VerificationStatus.REJECTED ||
                          c.FinalDecision == VerificationDecision.APPROVED ||
                          c.FinalDecision == VerificationDecision.REJECTED;

        var remainingSpan = dueAt - now;
        var remainingMinutes = (int)remainingSpan.TotalMinutes;
        var isOverdue = !isCompleted && remainingSpan <= TimeSpan.Zero;

        string formatted;
        if (isCompleted)
        {
            formatted = "Completed";
        }
        else if (isOverdue)
        {
            formatted = "Overdue";
        }
        else if (remainingSpan.TotalMinutes < 60)
        {
            formatted = $"{Math.Max(1, (int)remainingSpan.TotalMinutes)}m remaining";
        }
        else if (remainingSpan.TotalHours < 24)
        {
            var h = (int)remainingSpan.TotalHours;
            var m = remainingSpan.Minutes;
            formatted = m > 0 ? $"{h}h {m}m remaining" : $"{h}h remaining";
        }
        else
        {
            var d = (int)remainingSpan.TotalDays;
            var h = remainingSpan.Hours;
            formatted = h > 0 ? $"{d}d {h}h remaining" : $"{d}d remaining";
        }

        return new VerificationQueueSlaDto(dueAt, isOverdue, remainingMinutes, formatted);
    }
}
