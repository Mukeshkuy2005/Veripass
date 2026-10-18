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
    string? PassportId,
    string? Gstin,
    string? Pan,
    string? Cin,
    string VerificationStatus,
    int TrustScore,
    string RiskRating,
    string? Industry,
    string? State,
    int CredentialsCount,
    string? FlagReason,
    string? SuspensionReason,
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
            .Include(b => b.Passport)
            .Include(b => b.Addresses)
            .Include(b => b.Credentials)
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
            .ToListAsync(cancellationToken);

        var dtos = list.Select(b =>
        {
            var score = b.TrustScore?.OverallScore ?? (b.Passport?.VerificationScore ?? 0);
            var risk = SupplierRiskPolicy.DetermineRiskLevelSafe(score);
            var riskRating = risk == RiskLevel.CRITICAL || risk == RiskLevel.HIGH ? "HIGH" : (risk == RiskLevel.MEDIUM ? "MEDIUM" : "LOW");
            var primaryAddr = b.Addresses.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses.FirstOrDefault();
            var passportId = b.Passport?.PassportNumber ?? b.BusinessIdNumber;
            var status = b.Passport?.Status == "SUSPENDED" 
                ? "SUSPENDED" 
                : (b.VerificationStatus == VerificationStatus.FLAGGED ? "FLAGGED" : "ACTIVE");

            return new AdminBusinessSummaryDto(
                b.Id,
                b.LegalName,
                b.TradeName,
                b.BusinessIdNumber,
                passportId,
                b.Gstin,
                b.Pan,
                b.Cin,
                status,
                score,
                riskRating,
                b.BusinessType ?? "Manufacturing",
                primaryAddr?.State ?? "Delhi",
                b.Credentials.Count,
                b.VerificationStatus == VerificationStatus.FLAGGED ? "Flagged by compliance review" : null,
                b.Passport?.Status == "SUSPENDED" ? "Suspended by admin compliance" : null,
                b.CreatedAt);
        }).ToList();

        return Result<List<AdminBusinessSummaryDto>>.Success(dtos);
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

public record GetAdminBusinessByIdQuery(string Id) : IRequest<Result<AdminBusinessDetailDto>>;

public class GetAdminBusinessByIdQueryHandler : IRequestHandler<GetAdminBusinessByIdQuery, Result<AdminBusinessDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBusinessByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminBusinessDetailDto>> Handle(GetAdminBusinessByIdQuery request, CancellationToken cancellationToken)
    {
        Business? b = null;
        if (Guid.TryParse(request.Id, out var gId))
        {
            b = await _context.Businesses
                .Include(x => x.TrustScore)
                .Include(x => x.Credentials)
                .Include(x => x.Documents)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == gId || (x.Passport != null && x.Passport.Id == gId), cancellationToken);
        }

        if (b == null)
        {
            b = await _context.Businesses
                .Include(x => x.TrustScore)
                .Include(x => x.Credentials)
                .Include(x => x.Documents)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BusinessIdNumber == request.Id ||
                                         x.Gstin == request.Id ||
                                         x.Pan == request.Id ||
                                         (x.Passport != null && (x.Passport.PassportNumber == request.Id || x.Passport.PublicIdentifier == request.Id)),
                                     cancellationToken);
        }

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

public record SuspendBusinessCommand(string BusinessId, string Reason) : IRequest<Result<bool>>;

public class SuspendBusinessCommandHandler : IRequestHandler<SuspendBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SuspendBusinessCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(SuspendBusinessCommand request, CancellationToken cancellationToken)
    {
        Business? b = null;
        if (Guid.TryParse(request.BusinessId, out var gId))
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.Id == gId || (x.Passport != null && x.Passport.Id == gId), cancellationToken);
        }
        if (b == null)
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.BusinessIdNumber == request.BusinessId ||
                                         x.Gstin == request.BusinessId ||
                                         x.Pan == request.BusinessId ||
                                         (x.Passport != null && (x.Passport.PassportNumber == request.BusinessId || x.Passport.PublicIdentifier == request.BusinessId)),
                                     cancellationToken);
        }

        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.FLAGGED;
        if (b.Passport != null)
        {
            b.Passport.Status = "SUSPENDED";
            b.Passport.UpdatedAt = DateTime.UtcNow;
        }

        _context.RiskSignals.Add(new RiskSignal
        {
            BusinessId = b.Id,
            SignalType = "ADMIN_SUSPENSION",
            Severity = RiskLevel.CRITICAL,
            ScoreImpact = -50,
            Description = request.Reason,
            Status = "ACTIVE"
        });

        if (b.TrustScore != null)
        {
            b.TrustScore.OverallScore = Math.Max(0, b.TrustScore.OverallScore - 50);
            b.TrustScore.LastCalculatedAt = DateTime.UtcNow;
        }

        var rels = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == b.Id)
            .ToListAsync(cancellationToken);
        foreach (var rel in rels)
        {
            rel.RiskLevel = RiskLevel.CRITICAL;
            rel.UpdatedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Action = "BUSINESS_SUSPENDED",
            Category = "ADMIN_COMPLIANCE",
            ResourceType = "Business",
            ResourceId = b.Id.ToString(),
            TargetName = b.LegalName,
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NewValuesJson = $"{{\"reason\":\"{request.Reason}\",\"status\":\"SUSPENDED\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record ActivateBusinessCommand(string BusinessId) : IRequest<Result<bool>>;

public class ActivateBusinessCommandHandler : IRequestHandler<ActivateBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITrustScoreCalculator _trustScoreCalculator;
    private readonly ICurrentUserService _currentUserService;

    public ActivateBusinessCommandHandler(
        IApplicationDbContext context, 
        ITrustScoreCalculator trustScoreCalculator,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _trustScoreCalculator = trustScoreCalculator;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(ActivateBusinessCommand request, CancellationToken cancellationToken)
    {
        Business? b = null;
        if (Guid.TryParse(request.BusinessId, out var gId))
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.Id == gId || (x.Passport != null && x.Passport.Id == gId), cancellationToken);
        }
        if (b == null)
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.BusinessIdNumber == request.BusinessId ||
                                         x.Gstin == request.BusinessId ||
                                         x.Pan == request.BusinessId ||
                                         (x.Passport != null && (x.Passport.PassportNumber == request.BusinessId || x.Passport.PublicIdentifier == request.BusinessId)),
                                     cancellationToken);
        }

        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.VERIFIED;
        if (b.Passport != null)
        {
            b.Passport.Status = "ACTIVE";
            b.Passport.UpdatedAt = DateTime.UtcNow;
        }

        // Resolve active admin suspension or flag signals
        var activeSignals = await _context.RiskSignals
            .Where(r => r.BusinessId == b.Id && (r.SignalType == "ADMIN_SUSPENSION" || r.SignalType == "ADMIN_FLAGGED") && r.Status == "ACTIVE")
            .ToListAsync(cancellationToken);
        foreach (var sig in activeSignals)
        {
            sig.Status = "RESOLVED";
        }

        // Authoritative score recalculation
        var calc = await _trustScoreCalculator.CalculateAsync(b.Id, cancellationToken);
        if (b.TrustScore == null)
        {
            b.TrustScore = new VeriPass.Domain.Entities.TrustScore { BusinessId = b.Id };
            _context.TrustScores.Add(b.TrustScore);
        }
        b.TrustScore.OverallScore = calc.OverallScore;
        b.TrustScore.IdentityScore = calc.IdentityScore;
        b.TrustScore.CredentialScore = calc.CredentialScore;
        b.TrustScore.ComplianceScore = calc.ComplianceScore;
        b.TrustScore.VerificationScore = calc.VerificationScore;
        b.TrustScore.RiskScore = calc.RiskScore;
        b.TrustScore.LastCalculatedAt = DateTime.UtcNow;

        if (b.Passport != null)
        {
            b.Passport.VerificationScore = calc.OverallScore;
        }

        var derivedRisk = SupplierRiskPolicy.DetermineRiskLevelSafe(calc.OverallScore);
        var rels = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == b.Id)
            .ToListAsync(cancellationToken);
        foreach (var rel in rels)
        {
            rel.RiskLevel = derivedRisk;
            rel.UpdatedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Action = "BUSINESS_ACTIVATED",
            Category = "ADMIN_COMPLIANCE",
            ResourceType = "Business",
            ResourceId = b.Id.ToString(),
            TargetName = b.LegalName,
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NewValuesJson = $"{{\"status\":\"ACTIVE\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record FlagBusinessCommand(string BusinessId, string Reason) : IRequest<Result<bool>>;

public class FlagBusinessCommandHandler : IRequestHandler<FlagBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public FlagBusinessCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(FlagBusinessCommand request, CancellationToken cancellationToken)
    {
        Business? b = null;
        if (Guid.TryParse(request.BusinessId, out var gId))
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.Id == gId || (x.Passport != null && x.Passport.Id == gId), cancellationToken);
        }
        if (b == null)
        {
            b = await _context.Businesses
                .Include(x => x.Passport)
                .Include(x => x.TrustScore)
                .FirstOrDefaultAsync(x => x.BusinessIdNumber == request.BusinessId ||
                                         x.Gstin == request.BusinessId ||
                                         x.Pan == request.BusinessId ||
                                         (x.Passport != null && (x.Passport.PassportNumber == request.BusinessId || x.Passport.PublicIdentifier == request.BusinessId)),
                                     cancellationToken);
        }

        if (b == null) return Result<bool>.Failure("Business not found.");

        b.VerificationStatus = VerificationStatus.FLAGGED;
        if (b.Passport != null)
        {
            b.Passport.Status = "FLAGGED";
            b.Passport.UpdatedAt = DateTime.UtcNow;
        }

        _context.RiskSignals.Add(new RiskSignal
        {
            BusinessId = b.Id,
            SignalType = "ADMIN_FLAGGED",
            Severity = RiskLevel.HIGH,
            ScoreImpact = -25,
            Description = request.Reason,
            Status = "ACTIVE"
        });

        if (b.TrustScore != null)
        {
            b.TrustScore.OverallScore = Math.Max(0, b.TrustScore.OverallScore - 25);
            b.TrustScore.LastCalculatedAt = DateTime.UtcNow;
        }

        var rels = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == b.Id)
            .ToListAsync(cancellationToken);
        foreach (var rel in rels)
        {
            rel.RiskLevel = RiskLevel.HIGH;
            rel.UpdatedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Action = "BUSINESS_FLAGGED",
            Category = "ADMIN_COMPLIANCE",
            ResourceType = "Business",
            ResourceId = b.Id.ToString(),
            TargetName = b.LegalName,
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NewValuesJson = $"{{\"reason\":\"{request.Reason}\",\"status\":\"FLAGGED\"}}"
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
    int SuppliersCount,
    int VerificationRequestsCount,
    string Status,
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
            .Include(o => o.BuyerRelationships)
            .Include(o => o.SentVerificationRequests)
            .AsNoTracking()
            .Where(o => o.Type == OrganizationType.BUYER || o.Type == OrganizationType.BOTH)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminBuyerSummaryDto(
                o.Id,
                o.Name,
                o.Type.ToString(),
                o.Members.Count,
                o.BuyerRelationships.Count,
                o.SentVerificationRequests.Count,
                "ACTIVE",
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
    bool IsActive,
    string? CredentialType = null,
    string? ValidationType = null,
    bool IsRequired = true,
    int DefaultWeight = 0,
    string? RuleExpressionJson = null);

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
                r.IsActive,
                r.CredentialType,
                r.ValidationType,
                r.IsRequired,
                r.DefaultWeight,
                r.RuleExpressionJson))
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
    bool IsActive,
    string? CredentialType = null,
    string? ValidationType = null,
    bool IsRequired = true,
    int DefaultWeight = 0,
    string? RuleExpressionJson = null) : IRequest<Result<bool>>;

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

        if (!string.IsNullOrWhiteSpace(request.CredentialType)) rule.CredentialType = request.CredentialType.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.ValidationType)) rule.ValidationType = request.ValidationType.Trim().ToUpperInvariant();
        rule.IsRequired = request.IsRequired;
        rule.DefaultWeight = request.DefaultWeight;
        if (request.RuleExpressionJson != null) rule.RuleExpressionJson = request.RuleExpressionJson;

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
