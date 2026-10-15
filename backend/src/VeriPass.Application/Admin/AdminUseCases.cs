using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Admin;

// 1. Dashboard
public record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;

public record AdminDashboardDto(
    int TotalBusinesses,
    int TotalVerifiedPassports,
    int TotalDocuments,
    int TotalAuditLogs,
    int PendingVerifications,
    int ActiveRiskSignals,
    int OpenDisputes);

public class GetAdminDashboardQueryHandler : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminDashboardDto>> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var businesses = await _context.Businesses.CountAsync(cancellationToken);
        var passports = await _context.BusinessPassports.CountAsync(p => p.Status == "ACTIVE", cancellationToken);
        var documents = await _context.Documents.CountAsync(cancellationToken);
        var audits = await _context.AuditLogs.CountAsync(cancellationToken);
        var pendingVerifications = await _context.VerificationCases.CountAsync(c => c.Status == VerificationStatus.PENDING, cancellationToken);
        var activeRisks = await _context.RiskSignals.CountAsync(r => r.Status == "ACTIVE", cancellationToken);
        var openDisputes = await _context.Disputes.CountAsync(d => d.Status == DisputeStatus.OPEN, cancellationToken);

        return Result<AdminDashboardDto>.Success(new AdminDashboardDto(
            businesses, passports, documents, audits, pendingVerifications, activeRisks, openDisputes));
    }
}

// 2. Business Management
public record AdminBusinessSummaryDto(
    Guid Id,
    string LegalName,
    string? TradeName,
    string BusinessIdNumber,
    string? Gstin,
    string? Pan,
    string VerificationStatus,
    int TrustScore,
    DateTime CreatedAt);

public record GetAdminBusinessesQuery(
    string? Search = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<List<AdminBusinessSummaryDto>>>;

public class GetAdminBusinessesQueryHandler : IRequestHandler<GetAdminBusinessesQuery, Result<List<AdminBusinessSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBusinessesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminBusinessSummaryDto>>> Handle(GetAdminBusinessesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Businesses
            .Include(b => b.TrustScore)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLowerInvariant();
            query = query.Where(b => b.LegalName.ToLower().Contains(q) || (b.Gstin != null && b.Gstin.ToLower().Contains(q)) || b.BusinessIdNumber.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<VerificationStatus>(request.Status, true, out var vs))
        {
            query = query.Where(b => b.VerificationStatus == vs);
        }

        var list = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new AdminBusinessSummaryDto(
                b.Id,
                b.LegalName,
                b.TradeName,
                b.BusinessIdNumber,
                b.Gstin,
                b.Pan,
                b.VerificationStatus.ToString(),
                b.TrustScore != null ? b.TrustScore.OverallScore : 0,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminBusinessSummaryDto>>.Success(list);
    }
}

public record AdminBusinessDetailDto(
    Guid Id,
    Guid OrganizationId,
    string LegalName,
    string? TradeName,
    string BusinessIdNumber,
    string? BusinessType,
    string? Pan,
    string? Gstin,
    string? Cin,
    string? UdyamNumber,
    string VerificationStatus,
    int TrustScore,
    int CredentialCount,
    int DocumentCount,
    DateTime CreatedAt);

public record GetAdminBusinessByIdQuery(Guid Id) : IRequest<Result<AdminBusinessDetailDto>>;

public class GetAdminBusinessByIdQueryHandler : IRequestHandler<GetAdminBusinessByIdQuery, Result<AdminBusinessDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBusinessByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminBusinessDetailDto>> Handle(GetAdminBusinessByIdQuery request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses
            .Include(x => x.TrustScore)
            .Include(x => x.Credentials)
            .Include(x => x.Documents)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (b == null) return Result<AdminBusinessDetailDto>.Failure("Business not found.");

        return Result<AdminBusinessDetailDto>.Success(new AdminBusinessDetailDto(
            b.Id,
            b.OrganizationId,
            b.LegalName,
            b.TradeName,
            b.BusinessIdNumber,
            b.BusinessType,
            b.Pan,
            b.Gstin,
            b.Cin,
            b.UdyamNumber,
            b.VerificationStatus.ToString(),
            b.TrustScore?.OverallScore ?? 0,
            b.Credentials.Count,
            b.Documents.Count,
            b.CreatedAt));
    }
}

public record SuspendBusinessCommand(Guid BusinessId, string Reason) : IRequest<Result<bool>>;

public class SuspendBusinessCommandHandler : IRequestHandler<SuspendBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public SuspendBusinessCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(SuspendBusinessCommand request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses.FirstOrDefaultAsync(x => x.Id == request.BusinessId, cancellationToken);
        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.FLAGGED;
        _context.RiskSignals.Add(new RiskSignal
        {
            BusinessId = b.Id,
            SignalType = "ADMIN_SUSPENSION",
            Severity = RiskLevel.CRITICAL,
            ScoreImpact = -50,
            Description = request.Reason,
            Status = "ACTIVE"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record ActivateBusinessCommand(Guid BusinessId) : IRequest<Result<bool>>;

public class ActivateBusinessCommandHandler : IRequestHandler<ActivateBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public ActivateBusinessCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(ActivateBusinessCommand request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses.FirstOrDefaultAsync(x => x.Id == request.BusinessId, cancellationToken);
        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.VERIFIED;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record FlagBusinessCommand(Guid BusinessId, string Reason) : IRequest<Result<bool>>;

public class FlagBusinessCommandHandler : IRequestHandler<FlagBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public FlagBusinessCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(FlagBusinessCommand request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses.FirstOrDefaultAsync(x => x.Id == request.BusinessId, cancellationToken);
        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.FLAGGED;
        _context.RiskSignals.Add(new RiskSignal
        {
            BusinessId = b.Id,
            SignalType = "ADMIN_FLAGGED",
            Severity = RiskLevel.HIGH,
            ScoreImpact = -25,
            Description = request.Reason,
            Status = "ACTIVE"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 3. Buyers
public record AdminBuyerSummaryDto(
    Guid Id,
    string Name,
    string OrganizationType,
    int MemberCount,
    DateTime CreatedAt);

public record GetAdminBuyersQuery() : IRequest<Result<List<AdminBuyerSummaryDto>>>;

public class GetAdminBuyersQueryHandler : IRequestHandler<GetAdminBuyersQuery, Result<List<AdminBuyerSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBuyersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminBuyerSummaryDto>>> Handle(GetAdminBuyersQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.Organizations
            .Include(o => o.Members)
            .AsNoTracking()
            .Where(o => o.Type == OrganizationType.BUYER || o.Type == OrganizationType.BOTH)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminBuyerSummaryDto(
                o.Id,
                o.Name,
                o.Type.ToString(),
                o.Members.Count,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminBuyerSummaryDto>>.Success(list);
    }
}

// 4. Verifications
public record AdminVerificationSummaryDto(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    string VerificationType,
    string Status,
    string Priority,
    DateTime CreatedAt);

public record GetAdminVerificationsQuery(string? Status = null) : IRequest<Result<List<AdminVerificationSummaryDto>>>;

public class GetAdminVerificationsQueryHandler : IRequestHandler<GetAdminVerificationsQuery, Result<List<AdminVerificationSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminVerificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminVerificationSummaryDto>>> Handle(GetAdminVerificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VerificationCases
            .Include(c => c.Business)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<VerificationStatus>(request.Status, true, out var vs))
        {
            query = query.Where(c => c.Status == vs);
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .Select(c => new AdminVerificationSummaryDto(
                c.Id,
                c.BusinessId,
                c.Business.LegalName,
                c.VerificationType,
                c.Status.ToString(),
                c.Priority,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminVerificationSummaryDto>>.Success(list);
    }
}

// 5. Credential Types
public record CredentialTypeDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsRequired,
    bool RequiresExpiry,
    string? VerificationProvider,
    bool IsActive);

public record GetCredentialTypesQuery() : IRequest<Result<List<CredentialTypeDto>>>;

public class GetCredentialTypesQueryHandler : IRequestHandler<GetCredentialTypesQuery, Result<List<CredentialTypeDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCredentialTypesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CredentialTypeDto>>> Handle(GetCredentialTypesQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.CredentialTypes
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CredentialTypeDto(
                c.Id,
                c.Code,
                c.Name,
                c.Category,
                c.Description,
                c.IsRequired,
                c.RequiresExpiry,
                c.VerificationProvider,
                c.IsActive))
            .ToListAsync(cancellationToken);

        return Result<List<CredentialTypeDto>>.Success(list);
    }
}

public record CreateCredentialTypeCommand(
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsRequired,
    bool RequiresExpiry,
    string? VerificationProvider) : IRequest<Result<Guid>>;

public class CreateCredentialTypeCommandHandler : IRequestHandler<CreateCredentialTypeCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateCredentialTypeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateCredentialTypeCommand request, CancellationToken cancellationToken)
    {
        var ct = new CredentialType
        {
            Code = request.Code.ToUpperInvariant().Trim(),
            Name = request.Name.Trim(),
            Category = request.Category,
            Description = request.Description,
            IsRequired = request.IsRequired,
            RequiresExpiry = request.RequiresExpiry,
            VerificationProvider = request.VerificationProvider,
            IsActive = true
        };

        _context.CredentialTypes.Add(ct);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(ct.Id);
    }
}

public record UpdateCredentialTypeCommand(
    Guid Id,
    string Name,
    string Category,
    bool IsRequired,
    bool RequiresExpiry,
    string? VerificationProvider,
    string? Description,
    bool IsActive) : IRequest<Result<bool>>;

public class UpdateCredentialTypeCommandHandler : IRequestHandler<UpdateCredentialTypeCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCredentialTypeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateCredentialTypeCommand request, CancellationToken cancellationToken)
    {
        var ct = await _context.CredentialTypes.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (ct == null) return Result<bool>.Failure("Credential type not found.");

        ct.Name = request.Name;
        ct.Category = request.Category;
        ct.Description = request.Description;
        ct.IsRequired = request.IsRequired;
        ct.RequiresExpiry = request.RequiresExpiry;
        ct.VerificationProvider = request.VerificationProvider;
        ct.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 6. Compliance Rules
public record ComplianceRuleDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string Severity,
    string Description,
    bool IsActive);

public record GetComplianceRulesQuery() : IRequest<Result<List<ComplianceRuleDto>>>;

public class GetComplianceRulesQueryHandler : IRequestHandler<GetComplianceRulesQuery, Result<List<ComplianceRuleDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetComplianceRulesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ComplianceRuleDto>>> Handle(GetComplianceRulesQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.ComplianceRules
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new ComplianceRuleDto(
                r.Id,
                r.Code,
                r.Name,
                r.Category,
                r.Severity.ToString(),
                r.Description,
                r.IsActive))
            .ToListAsync(cancellationToken);

        return Result<List<ComplianceRuleDto>>.Success(list);
    }
}

public record UpdateComplianceRuleCommand(
    Guid Id,
    string Name,
    string Category,
    ComplianceSeverity Severity,
    string Description,
    bool IsActive) : IRequest<Result<bool>>;

public class UpdateComplianceRuleCommandHandler : IRequestHandler<UpdateComplianceRuleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateComplianceRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateComplianceRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _context.ComplianceRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (rule == null) return Result<bool>.Failure("Rule not found.");

        rule.Name = request.Name;
        rule.Category = request.Category;
        rule.Severity = request.Severity;
        rule.Description = request.Description;
        rule.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 7. Reports & Audit
public record AdminReportsDto(
    int TotalBusinesses,
    int VerifiedCount,
    int PendingCount,
    int FlaggedCount,
    double AverageTrustScore,
    int TotalVerificationsExecuted,
    int TotalRiskSignalsResolved);

public record GetAdminReportsQuery() : IRequest<Result<AdminReportsDto>>;

public class GetAdminReportsQueryHandler : IRequestHandler<GetAdminReportsQuery, Result<AdminReportsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminReportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminReportsDto>> Handle(GetAdminReportsQuery request, CancellationToken cancellationToken)
    {
        var total = await _context.Businesses.CountAsync(cancellationToken);
        var verified = await _context.Businesses.CountAsync(b => b.VerificationStatus == VerificationStatus.VERIFIED, cancellationToken);
        var pending = await _context.Businesses.CountAsync(b => b.VerificationStatus == VerificationStatus.PENDING, cancellationToken);
        var flagged = await _context.Businesses.CountAsync(b => b.VerificationStatus == VerificationStatus.FLAGGED, cancellationToken);

        var avgScore = await _context.TrustScores.Select(t => (double?)t.OverallScore).AverageAsync(cancellationToken) ?? 0;
        var totalVerifications = await _context.VerificationCases.CountAsync(cancellationToken);
        var totalResolvedRisks = await _context.RiskSignals.CountAsync(r => r.Status == "RESOLVED", cancellationToken);

        return Result<AdminReportsDto>.Success(new AdminReportsDto(
            total,
            verified,
            pending,
            flagged,
            Math.Round(avgScore, 1),
            totalVerifications,
            totalResolvedRisks));
    }
}

public record AuditLogEntryDto(
    Guid Id,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? IpAddress,
    DateTime CreatedAt);

public record GetAuditLogsQuery(int Page = 1, int PageSize = 50) : IRequest<Result<List<AuditLogEntryDto>>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<List<AuditLogEntryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AuditLogEntryDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogEntryDto(
                a.Id,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.IpAddress,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AuditLogEntryDto>>.Success(list);
    }
}

// 8. User Management
public record AdminUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    bool EmailVerified,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

public record GetAdminUsersQuery() : IRequest<Result<List<AdminUserDto>>>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, Result<List<AdminUserDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminUserDto>>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(100)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Status,
                u.EmailVerified,
                u.LastLoginAt,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminUserDto>>.Success(list);
    }
}

public record UpdateAdminUserCommand(Guid UserId, string Status) : IRequest<Result<bool>>;

public class UpdateAdminUserCommandHandler : IRequestHandler<UpdateAdminUserCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateAdminUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (u == null) return Result<bool>.Failure("User not found.");

        u.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
