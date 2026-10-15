using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.Audit;

// ==========================================
// DTOs
// ==========================================
public record BuyerActivityUserDto(
    Guid Id,
    string Name,
    string Role,
    string Email);

public record BuyerActivityTargetDto(
    string Type,
    string? Id,
    string? Name);

public record BuyerActivityOrgDto(
    Guid Id,
    string Name);

public record BuyerActivitySecurityDto(
    string? IpAddress,
    string? Device,
    string? Session,
    string? RequestId,
    bool IsRestricted);

public record BuyerActivityItemDto(
    Guid Id,
    string EventCode,
    DateTime Timestamp,
    BuyerActivityUserDto User,
    string Action,
    string ActionLabel,
    string Category,
    BuyerActivityTargetDto Target,
    string Result,
    string ResultLabel);

public record BuyerActivityPagedResultDto(
    List<BuyerActivityItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record BuyerActivitySummaryDto(
    int Today,
    int Success,
    int Failed,
    int Denied,
    int Total);

public record BuyerActivityDetailDto(
    Guid Id,
    string EventCode,
    DateTime Timestamp,
    BuyerActivityUserDto User,
    BuyerActivityOrgDto Organization,
    string Action,
    string ActionLabel,
    string Category,
    BuyerActivityTargetDto Target,
    string Result,
    string ResultLabel,
    BuyerActivitySecurityDto SecurityContext,
    string? OldValuesJson,
    string? NewValuesJson,
    string? MetadataJson);

public record ExportFileDto(
    string FileName,
    string ContentType,
    byte[] Data);

// ==========================================
// 1. GET BUYER ACTIVITY QUERY
// ==========================================
public record GetBuyerActivityQuery(
    Guid OrganizationId,
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    DateTime? From = null,
    DateTime? To = null,
    Guid? UserId = null,
    string? Category = null,
    string? Action = null,
    string? Target = null,
    string? Result = null,
    string? Sort = "timestamp_desc"
) : IRequest<Result<BuyerActivityPagedResultDto>>;

public class GetBuyerActivityQueryHandler : IRequestHandler<GetBuyerActivityQuery, Result<BuyerActivityPagedResultDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerActivityQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BuyerActivityPagedResultDto>> Handle(GetBuyerActivityQuery request, CancellationToken cancellationToken)
    {
        // 1. Strict Tenant Isolation
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
                .ThenInclude(u => u!.Memberships)
                    .ThenInclude(m => m.Role)
            .Where(a => a.OrganizationId == request.OrganizationId);

        // 2. Server-side Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(a =>
                (a.EventCode != null && a.EventCode.ToLower().Contains(s)) ||
                a.Action.ToLower().Contains(s) ||
                (a.TargetName != null && a.TargetName.ToLower().Contains(s)) ||
                (a.ResourceId != null && a.ResourceId.ToLower().Contains(s)) ||
                (a.User != null && (
                    (a.User.FirstName + " " + a.User.LastName).ToLower().Contains(s) ||
                    a.User.Email.ToLower().Contains(s)
                ))
            );
        }

        // 3. Date Range
        if (request.From.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(request.From.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(request.To.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt <= toUtc);
        }

        // 4. Actor User Filter
        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        // 5. Category Filter
        if (!string.IsNullOrWhiteSpace(request.Category) && !request.Category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var cat = request.Category.Trim().ToUpperInvariant();
            query = query.Where(a => a.Category == cat);
        }

        // 6. Action Filter
        if (!string.IsNullOrWhiteSpace(request.Action) && !request.Action.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var act = request.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == act);
        }

        // 7. Target Filter
        if (!string.IsNullOrWhiteSpace(request.Target) && !request.Target.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var tgt = request.Target.Trim().ToLower();
            query = query.Where(a => (a.TargetName != null && a.TargetName.ToLower().Contains(tgt)) ||
                                     a.ResourceType.ToLower().Contains(tgt));
        }

        // 8. Result Filter
        if (!string.IsNullOrWhiteSpace(request.Result) && !request.Result.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var res = request.Result.Trim().ToUpperInvariant();
            query = query.Where(a => a.Result == res);
        }

        // 9. Total Count
        var totalCount = await query.CountAsync(cancellationToken);

        // 10. Sorting
        query = request.Sort?.ToLower() switch
        {
            "timestamp_asc" => query.OrderBy(a => a.CreatedAt),
            "user_asc" => query.OrderBy(a => a.User != null ? a.User.FirstName : ""),
            "user_desc" => query.OrderByDescending(a => a.User != null ? a.User.FirstName : ""),
            "action_asc" => query.OrderBy(a => a.Action),
            "action_desc" => query.OrderByDescending(a => a.Action),
            "target_asc" => query.OrderBy(a => a.TargetName ?? a.ResourceType),
            "target_desc" => query.OrderByDescending(a => a.TargetName ?? a.ResourceType),
            _ => query.OrderByDescending(a => a.CreatedAt)
        };

        // 11. Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = logs.Select(a =>
        {
            var userRole = a.User?.Memberships.FirstOrDefault(m => m.OrganizationId == request.OrganizationId)?.Role?.Name
                           ?? a.User?.Memberships.FirstOrDefault()?.Role?.Name
                           ?? "Procurement Team";

            var fullName = a.User != null
                ? $"{a.User.FirstName} {a.User.LastName}".Trim()
                : "System / Automated Service";

            return new BuyerActivityItemDto(
                Id: a.Id,
                EventCode: a.EventCode ?? $"AUD-{a.Id.ToString("N")[..6].ToUpperInvariant()}",
                Timestamp: a.CreatedAt,
                User: new BuyerActivityUserDto(
                    Id: a.UserId ?? Guid.Empty,
                    Name: fullName,
                    Role: userRole,
                    Email: a.User?.Email ?? "system@veripass.in"
                ),
                Action: a.Action,
                ActionLabel: FormatActionLabel(a.Action),
                Category: a.Category,
                Target: new BuyerActivityTargetDto(
                    Type: a.ResourceType,
                    Id: a.ResourceId,
                    Name: a.TargetName ?? a.ResourceId ?? a.ResourceType
                ),
                Result: a.Result,
                ResultLabel: FormatResultLabel(a.Result)
            );
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Result<BuyerActivityPagedResultDto>.Success(new BuyerActivityPagedResultDto(
            items,
            page,
            pageSize,
            totalCount,
            totalPages
        ));
    }

    private static string FormatActionLabel(string action)
    {
        return action.ToUpperInvariant() switch
        {
            "ONBOARDING_APPROVED" or "BUYER_APPROVED" => "Approved Supplier Onboarding",
            "ONBOARDING_REJECTED" => "Rejected Onboarding Request",
            "ONBOARDING_CREATED" => "Initiated Onboarding Request",
            "ONBOARDING_SENT" => "Dispatched Onboarding Invite",
            "ONBOARDING_VIEWED" => "Inspected Onboarding Request",
            "ONBOARDING_WITHDRAWN" => "Withdrew Onboarding Request",
            "ONBOARDING_REMINDER_SENT" => "Dispatched Supplier Reminder",
            "PASSPORT_VIEWED" => "Inspected Business Passport",
            "PASSPORT_SHARED" => "Shared Sovereign Passport",
            "PASSPORT_EXPORT_REQUESTED" => "Exported Passport Dossier",
            "PASSPORT_ACCESS_REVOKED" => "Revoked Passport Authorization",
            "SUPPLIER_VIEWED" => "Viewed Supplier Profile",
            "SUPPLIER_SEARCHED" => "Executed Supplier Directory Search",
            "SUPPLIER_SHORTLISTED" => "Shortlisted Sovereign Supplier",
            "SUPPLIER_REMOVED_FROM_SHORTLIST" => "Removed Supplier from Shortlist",
            "VERIFICATION_REQUESTED" => "Dispatched Statutory Verification",
            "VERIFICATION_VIEWED" => "Inspected Verification Case",
            "VERIFICATION_COMPLETED" => "Verified Statutory Registry",
            "VERIFICATION_FAILED" => "Statutory Registry Verification Failed",
            "CREDENTIAL_VIEWED" => "Reviewed Business Credential",
            "CREDENTIAL_VERIFIED" => "Cryptographically Verified Credential",
            "USER_LOGIN" => "Authenticated to Workspace",
            "USER_REGISTERED" => "Registered Organization User",
            "AUDIT_LOG_EXPORTED" => "Exported Forensic Audit Ledger",
            _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(action.Replace('_', ' ').ToLowerInvariant())
        };
    }

    private static string FormatResultLabel(string result)
    {
        return result.ToUpperInvariant() switch
        {
            "SUCCESS" => "Success",
            "FAILED" => "Failed",
            "DENIED" => "Denied",
            _ => result
        };
    }
}

// ==========================================
// 2. GET BUYER ACTIVITY SUMMARY QUERY
// ==========================================
public record GetBuyerActivitySummaryQuery(Guid OrganizationId) : IRequest<Result<BuyerActivitySummaryDto>>;

public class GetBuyerActivitySummaryQueryHandler : IRequestHandler<GetBuyerActivitySummaryQuery, Result<BuyerActivitySummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerActivitySummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BuyerActivitySummaryDto>> Handle(GetBuyerActivitySummaryQuery request, CancellationToken cancellationToken)
    {
        var todayUtcStart = DateTime.UtcNow.Date;

        var counts = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == request.OrganizationId)
            .GroupBy(a => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Today = g.Count(a => a.CreatedAt >= todayUtcStart),
                Success = g.Count(a => a.Result == "SUCCESS"),
                Failed = g.Count(a => a.Result == "FAILED"),
                Denied = g.Count(a => a.Result == "DENIED")
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (counts == null)
        {
            return Result<BuyerActivitySummaryDto>.Success(new BuyerActivitySummaryDto(0, 0, 0, 0, 0));
        }

        return Result<BuyerActivitySummaryDto>.Success(new BuyerActivitySummaryDto(
            Today: counts.Today,
            Success: counts.Success,
            Failed: counts.Failed,
            Denied: counts.Denied,
            Total: counts.Total
        ));
    }
}

// ==========================================
// 3. GET BUYER ACTIVITY DETAIL QUERY
// ==========================================
public record GetBuyerActivityDetailQuery(
    string IdOrCode,
    Guid BuyerOrganizationId,
    bool IsAdmin
) : IRequest<Result<BuyerActivityDetailDto>>
{
    public GetBuyerActivityDetailQuery(Guid Id, Guid BuyerOrganizationId, bool IsAdmin)
        : this(Id.ToString(), BuyerOrganizationId, IsAdmin) { }
}

public class GetBuyerActivityDetailQueryHandler : IRequestHandler<GetBuyerActivityDetailQuery, Result<BuyerActivityDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerActivityDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BuyerActivityDetailDto>> Handle(GetBuyerActivityDetailQuery request, CancellationToken cancellationToken)
    {
        bool isGuid = Guid.TryParse(request.IdOrCode, out var guidId);

        var item = await _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
                .ThenInclude(u => u!.Memberships)
                    .ThenInclude(m => m.Role)
            .Include(a => a.Organization)
            .FirstOrDefaultAsync(a => (isGuid && a.Id == guidId) || (a.EventCode != null && a.EventCode == request.IdOrCode), cancellationToken);

        if (item == null)
            return Result<BuyerActivityDetailDto>.Failure("Audit event not found.");

        // Strict Tenant Isolation
        if (item.OrganizationId != request.BuyerOrganizationId)
            return Result<BuyerActivityDetailDto>.Failure("Unauthorized. You do not have permission to inspect this audit event.");

        var userRole = item.User?.Memberships.FirstOrDefault(m => m.OrganizationId == request.BuyerOrganizationId)?.Role?.Name
                       ?? item.User?.Memberships.FirstOrDefault()?.Role?.Name
                       ?? "Procurement Team";

        var fullName = item.User != null
            ? $"{item.User.FirstName} {item.User.LastName}".Trim()
            : "System / Automated Service";

        // Security Context - Enforced Server-Side based on RBAC privilege
        BuyerActivitySecurityDto sec;
        if (request.IsAdmin)
        {
            sec = new BuyerActivitySecurityDto(
                IpAddress: item.IpAddress ?? "127.0.0.1 (Local Gateway)",
                Device: item.UserAgent ?? "VeriPass Enterprise Client",
                Session: item.CorrelationId ?? "VPN-TLS-SECURE-SESSION",
                RequestId: item.CorrelationId ?? "REQ-" + item.Id.ToString("N")[..8].ToUpperInvariant(),
                IsRestricted: false
            );
        }
        else
        {
            sec = new BuyerActivitySecurityDto(
                IpAddress: null,
                Device: null,
                Session: null,
                RequestId: "REDACTED",
                IsRestricted: true
            );
        }

        return Result<BuyerActivityDetailDto>.Success(new BuyerActivityDetailDto(
            Id: item.Id,
            EventCode: item.EventCode ?? $"AUD-{item.Id.ToString("N")[..6].ToUpperInvariant()}",
            Timestamp: item.CreatedAt,
            User: new BuyerActivityUserDto(
                Id: item.UserId ?? Guid.Empty,
                Name: fullName,
                Role: userRole,
                Email: item.User?.Email ?? "system@veripass.in"
            ),
            Organization: new BuyerActivityOrgDto(
                Id: item.Organization?.Id ?? request.BuyerOrganizationId,
                Name: item.Organization?.Name ?? "Buyer Organization"
            ),
            Action: item.Action,
            ActionLabel: item.Action.Replace('_', ' '),
            Category: item.Category,
            Target: new BuyerActivityTargetDto(
                Type: item.ResourceType,
                Id: item.ResourceId,
                Name: item.TargetName ?? item.ResourceId ?? item.ResourceType
            ),
            Result: item.Result,
            ResultLabel: item.Result,
            SecurityContext: sec,
            OldValuesJson: item.OldValuesJson,
            NewValuesJson: item.NewValuesJson,
            MetadataJson: item.MetadataJson
        ));
    }
}

// ==========================================
// 4. EXPORT BUYER ACTIVITY COMMAND
// ==========================================
public record ExportBuyerActivityCommand(
    Guid OrganizationId,
    Guid UserId,
    string Format = "CSV",
    string? Search = null,
    DateTime? From = null,
    DateTime? To = null,
    Guid? FilterUserId = null,
    string? Category = null,
    string? Action = null,
    string? Target = null,
    string? Result = null
) : IRequest<Result<ExportFileDto>>;

public class ExportBuyerActivityCommandHandler : IRequestHandler<ExportBuyerActivityCommand, Result<ExportFileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ExportBuyerActivityCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Result<ExportFileDto>> Handle(ExportBuyerActivityCommand request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.OrganizationId == request.OrganizationId);

        // Filters applied server-side
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(a =>
                (a.EventCode != null && a.EventCode.ToLower().Contains(s)) ||
                a.Action.ToLower().Contains(s) ||
                (a.TargetName != null && a.TargetName.ToLower().Contains(s)) ||
                (a.ResourceId != null && a.ResourceId.ToLower().Contains(s)) ||
                (a.User != null && (
                    (a.User.FirstName + " " + a.User.LastName).ToLower().Contains(s) ||
                    a.User.Email.ToLower().Contains(s)
                ))
            );
        }

        if (request.From.HasValue)
            query = query.Where(a => a.CreatedAt >= DateTime.SpecifyKind(request.From.Value.Date, DateTimeKind.Utc));

        if (request.To.HasValue)
            query = query.Where(a => a.CreatedAt <= DateTime.SpecifyKind(request.To.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));

        if (request.FilterUserId.HasValue && request.FilterUserId.Value != Guid.Empty)
            query = query.Where(a => a.UserId == request.FilterUserId.Value);

        if (!string.IsNullOrWhiteSpace(request.Category) && !request.Category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(a => a.Category == request.Category.Trim().ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(request.Action) && !request.Action.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(a => a.Action == request.Action.Trim().ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(request.Result) && !request.Result.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(a => a.Result == request.Result.Trim().ToUpperInvariant());

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);

        byte[] fileBytes;
        string fileName;
        string contentType;

        var timeStampStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        if (request.Format.Equals("JSON", StringComparison.OrdinalIgnoreCase))
        {
            var jsonItems = logs.Select(a => new
            {
                EventCode = a.EventCode ?? $"AUD-{a.Id.ToString("N")[..6].ToUpperInvariant()}",
                Timestamp = a.CreatedAt.ToString("o"),
                User = a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : "System",
                UserEmail = a.User?.Email,
                Action = a.Action,
                Category = a.Category,
                TargetType = a.ResourceType,
                TargetId = a.ResourceId,
                TargetName = a.TargetName,
                Result = a.Result,
                IpAddress = a.IpAddress
            });

            fileBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(jsonItems, new JsonSerializerOptions { WriteIndented = true }));
            fileName = $"Buyer_Audit_Log_{timeStampStr}.json";
            contentType = "application/json";
        }
        else
        {
            // Default CSV
            var sb = new StringBuilder();
            sb.AppendLine("Event ID,Timestamp (UTC),User Name,User Email,Category,Action,Target Type,Target ID,Target Name,Result,IP Address");

            foreach (var a in logs)
            {
                var evCode = a.EventCode ?? $"AUD-{a.Id.ToString("N")[..6].ToUpperInvariant()}";
                var ts = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                var uName = a.User != null ? $"{a.User.FirstName} {a.User.LastName}".Replace(",", " ") : "System";
                var uEmail = a.User?.Email ?? "";
                var cat = a.Category;
                var act = a.Action;
                var tgtType = a.ResourceType;
                var tgtId = a.ResourceId ?? "";
                var tgtName = (a.TargetName ?? "").Replace(",", " ");
                var res = a.Result;
                var ip = a.IpAddress ?? "";

                sb.AppendLine($"\"{evCode}\",\"{ts}\",\"{uName}\",\"{uEmail}\",\"{cat}\",\"{act}\",\"{tgtType}\",\"{tgtId}\",\"{tgtName}\",\"{res}\",\"{ip}\"");
            }

            fileBytes = Encoding.UTF8.GetBytes(sb.ToString());
            fileName = $"Buyer_Audit_Log_{timeStampStr}.csv";
            contentType = "text/csv";
        }

        // Section 28: Export MUST itself be audited!
        await _auditService.RecordAsync(new RecordAuditOptions(
            Action: "AUDIT_LOG_EXPORTED",
            Category: "SECURITY",
            TargetType: "AUDIT_LOG",
            TargetId: request.OrganizationId.ToString(),
            TargetName: $"Forensic Export ({request.Format.ToUpperInvariant()} - {logs.Count} records)",
            Result: "SUCCESS",
            OverrideOrganizationId: request.OrganizationId,
            OverrideUserId: request.UserId
        ), cancellationToken);

        return Result<ExportFileDto>.Success(new ExportFileDto(fileName, contentType, fileBytes));
    }
}

// ==========================================
// 5. GET BUYER ACTIVITY USERS QUERY
// ==========================================
public record GetBuyerActivityUsersQuery(Guid OrganizationId) : IRequest<Result<List<BuyerActivityUserDto>>>;

public class GetBuyerActivityUsersQueryHandler : IRequestHandler<GetBuyerActivityUsersQuery, Result<List<BuyerActivityUserDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerActivityUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<BuyerActivityUserDto>>> Handle(GetBuyerActivityUsersQuery request, CancellationToken cancellationToken)
    {
        // Get all members of the Buyer Organization
        var members = await _context.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Role)
            .Where(m => m.OrganizationId == request.OrganizationId && m.User != null)
            .Select(m => new BuyerActivityUserDto(
                m.UserId,
                $"{m.User!.FirstName} {m.User.LastName}".Trim(),
                m.Role != null ? m.Role.Name : "Procurement Team",
                m.User.Email
            ))
            .Distinct()
            .ToListAsync(cancellationToken);

        // Also check if any other users have audit records for this org
        var auditUserIds = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == request.OrganizationId && a.UserId != null)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingIds = members.Select(m => m.Id).ToHashSet();
        var missingIds = auditUserIds.Where(id => !existingIds.Contains(id)).ToList();

        if (missingIds.Any())
        {
            var additionalUsers = await _context.Users
                .AsNoTracking()
                .Where(u => missingIds.Contains(u.Id))
                .Select(u => new BuyerActivityUserDto(
                    u.Id,
                    $"{u.FirstName} {u.LastName}".Trim(),
                    "Authorized User",
                    u.Email
                ))
                .ToListAsync(cancellationToken);

            members.AddRange(additionalUsers);
        }

        return Result<List<BuyerActivityUserDto>>.Success(members);
    }
}
