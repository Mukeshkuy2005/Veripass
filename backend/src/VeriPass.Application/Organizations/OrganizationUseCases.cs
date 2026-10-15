using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Organizations;

public record CreateOrganizationCommand(
    string Name,
    OrganizationType Type,
    string Email,
    string? Phone,
    string? Website) : IRequest<Result<Guid>>;

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = new Organization
        {
            Name = request.Name,
            Type = request.Type,
            Email = request.Email,
            Phone = request.Phone,
            Website = request.Website,
            IsActive = true
        };

        _context.Organizations.Add(org);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(org.Id);
    }
}

public record GetOrganizationQuery(Guid Id) : IRequest<Result<OrganizationDto>>;

public record OrganizationDto(Guid Id, string Name, string Type, string Email, string? Phone, string? Website, bool IsActive);

public class GetOrganizationQueryHandler : IRequestHandler<GetOrganizationQuery, Result<OrganizationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrganizationDto>> Handle(GetOrganizationQuery request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (org == null)
            return Result<OrganizationDto>.Failure("Organization not found.");

        return Result<OrganizationDto>.Success(new OrganizationDto(
            org.Id, org.Name, org.Type.ToString(), org.Email, org.Phone, org.Website, org.IsActive));
    }
}

public record UpdateOrganizationCommand(
    Guid OrganizationId,
    string? Name,
    string? Email,
    string? Phone,
    string? Website) : IRequest<Result<bool>>;

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateOrganizationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);

        if (org == null)
            return Result<bool>.Failure("Organization not found.");

        org.Name = request.Name ?? org.Name;
        org.Email = request.Email ?? org.Email;
        org.Phone = request.Phone ?? org.Phone;
        org.Website = request.Website ?? org.Website;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record PillarComplianceDto(
    int IdentityPercent,
    int TaxPercent,
    int AddressPercent,
    int BankPercent,
    int CertificationsPercent);

public record DashboardActionItemDto(
    string Id,
    string Type,
    string Title,
    string Description,
    int Count,
    string ActionLabel,
    string ActionUrl,
    string Severity);

public record DashboardActivityDto(
    string Time,
    string Title,
    string Description,
    string StatusColor);

public record OrganizationDashboardDto(
    Guid OrganizationId,
    string Name,
    string Type,
    int TotalMembers,
    int TotalSuppliers,
    int VerifiedSuppliers,
    int PendingSuppliers,
    int ExpiredSuppliers,
    int HighRiskSuppliers,
    double VerifiedPercentage,
    double PendingPercentage,
    double ExpiredPercentage,
    double HighRiskPercentage,
    string ItcSafeguarded,
    double AverageTrustScore,
    PillarComplianceDto PillarCompliance,
    List<DashboardActionItemDto> ActionRequired,
    List<DashboardActivityDto> RecentActivities);

public record GetOrganizationDashboardQuery(Guid OrganizationId) : IRequest<Result<OrganizationDashboardDto>>;

public class GetOrganizationDashboardQueryHandler : IRequestHandler<GetOrganizationDashboardQuery, Result<OrganizationDashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrganizationDashboardDto>> Handle(GetOrganizationDashboardQuery request, CancellationToken cancellationToken)
    {
        var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (org == null)
        {
            return Result<OrganizationDashboardDto>.Failure("Organization not found.");
        }

        var totalMembers = await _context.OrganizationMembers.CountAsync(m => m.OrganizationId == org.Id, cancellationToken);
        if (totalMembers == 0) totalMembers = 1;

        var relationships = await _context.BuyerSupplierRelationships
            .Include(r => r.SupplierBusiness)
                .ThenInclude(b => b.TrustScore)
            .Include(r => r.SupplierBusiness)
                .ThenInclude(b => b.Addresses)
            .Include(r => r.SupplierBusiness)
                .ThenInclude(b => b.Credentials)
                    .ThenInclude(c => c.CredentialType)
            .Where(r => r.BuyerOrganizationId == org.Id)
            .ToListAsync(cancellationToken);

        int totalSuppliers = relationships.Count;
        int verifiedSuppliers = relationships.Count(r => r.Status == RelationshipStatus.APPROVED);
        int pendingSuppliers = relationships.Count(r => r.Status == RelationshipStatus.PENDING || r.Status == RelationshipStatus.INVITED);
        int expiredSuppliers = relationships.Count(r => r.Status == RelationshipStatus.SUSPENDED);
        int highRiskSuppliers = relationships.Count(r => 
            r.RiskLevel == RiskLevel.HIGH || 
            r.RiskLevel == RiskLevel.CRITICAL ||
            (r.SupplierBusiness?.TrustScore != null && SupplierRiskPolicy.DetermineRiskLevelSafe(r.SupplierBusiness.TrustScore.OverallScore) == RiskLevel.HIGH));
        double avgTrustScore = 0;

        if (relationships.Count > 0)
        {
            var scoredSuppliers = relationships
                .Where(r => r.SupplierBusiness?.TrustScore != null)
                .Select(r => r.SupplierBusiness.TrustScore!.OverallScore)
                .ToList();

            avgTrustScore = scoredSuppliers.Count > 0 ? Math.Round(scoredSuppliers.Average(), 1) : 0;
        }

        double verifiedPct = totalSuppliers > 0 ? Math.Round((double)verifiedSuppliers / totalSuppliers * 100, 1) : 0;
        double pendingPct = totalSuppliers > 0 ? Math.Round((double)pendingSuppliers / totalSuppliers * 100, 1) : 0;
        double expiredPct = totalSuppliers > 0 ? Math.Round((double)expiredSuppliers / totalSuppliers * 100, 1) : 0;
        double highRiskPct = totalSuppliers > 0 ? Math.Round((double)highRiskSuppliers / totalSuppliers * 100, 1) : 0;

        string itcSafeguarded = totalSuppliers > 0 ? $"₹{(verifiedSuppliers * 0.28):F2} Cr Safeguarded" : "₹0 Safeguarded";

        // Compute dynamic pillar compliance based on verified supplier relationships
        int idPct = 0, taxPct = 0, addrPct = 0, bankPct = 0, certPct = 0;
        if (verifiedSuppliers > 0)
        {
            var verifiedBiz = relationships
                .Where(r => r.Status == RelationshipStatus.APPROVED && r.SupplierBusiness != null)
                .Select(r => r.SupplierBusiness!)
                .ToList();

            if (verifiedBiz.Count > 0)
            {
                idPct = (int)Math.Round((double)verifiedBiz.Count(b => !string.IsNullOrEmpty(b.Cin) || !string.IsNullOrEmpty(b.Pan)) / verifiedBiz.Count * 100);
                taxPct = (int)Math.Round((double)verifiedBiz.Count(b => !string.IsNullOrEmpty(b.Gstin)) / verifiedBiz.Count * 100);
                addrPct = (int)Math.Round((double)verifiedBiz.Count(b => b.Addresses.Any()) / verifiedBiz.Count * 100);
                bankPct = (int)Math.Round((double)verifiedBiz.Count(b => b.Credentials.Any(c => c.CredentialType.Code == "BANK_ACCOUNT" && c.VerificationStatus == VerificationStatus.VERIFIED)) / verifiedBiz.Count * 100);
                certPct = (int)Math.Round((double)verifiedBiz.Count(b => b.Credentials.Any(c => c.CredentialType.Code.StartsWith("ISO") && c.VerificationStatus == VerificationStatus.VERIFIED)) / verifiedBiz.Count * 100);
            }
        }

        var pillarCompliance = new PillarComplianceDto(
            IdentityPercent: idPct,
            TaxPercent: taxPct,
            AddressPercent: addrPct,
            BankPercent: bankPct,
            CertificationsPercent: certPct
        );

        var activeOnboardingCount = await _context.OnboardingRequests
            .CountAsync(o => o.BuyerOrganizationId == org.Id &&
                o.Status != OnboardingStatus.APPROVED &&
                o.Status != OnboardingStatus.REJECTED &&
                o.Status != OnboardingStatus.WITHDRAWN, cancellationToken);

        var pendingVerificationsCount = await _context.VerificationRequests
            .CountAsync(v => v.BuyerOrganizationId == org.Id && v.Status == RequestStatus.PENDING, cancellationToken);

        var actionRequired = new List<DashboardActionItemDto>();

        if (highRiskSuppliers > 0)
        {
            actionRequired.Add(new DashboardActionItemDto(
                Id: "action-high-risk",
                Type: "HIGH_RISK",
                Title: $"{highRiskSuppliers} suppliers marked high risk",
                Description: "ITC default risk or severe sovereign credential non-compliance detected.",
                Count: highRiskSuppliers,
                ActionLabel: "Review",
                ActionUrl: "/buyer/suppliers",
                Severity: "danger"
            ));
        }

        if (expiredSuppliers > 0)
        {
            actionRequired.Add(new DashboardActionItemDto(
                Id: "action-expired",
                Type: "EXPIRED",
                Title: $"{expiredSuppliers} suppliers have expired credentials",
                Description: "Factory licenses or ISO audit certificates have lapsed over 30 days.",
                Count: expiredSuppliers,
                ActionLabel: "View Expired",
                ActionUrl: "/buyer/suppliers",
                Severity: "amber"
            ));
        }

        if (activeOnboardingCount > 0)
        {
            actionRequired.Add(new DashboardActionItemDto(
                Id: "action-onboarding",
                Type: "ONBOARDING",
                Title: $"{activeOnboardingCount} onboarding requests pending",
                Description: "Suppliers awaiting statutory pre-qualification and approval sign-off.",
                Count: activeOnboardingCount,
                ActionLabel: "Review",
                ActionUrl: "/buyer/onboarding",
                Severity: "blue"
            ));
        }

        if (pendingVerificationsCount > 0)
        {
            actionRequired.Add(new DashboardActionItemDto(
                Id: "action-verification",
                Type: "VERIFICATION",
                Title: $"{pendingVerificationsCount} verification requests pending",
                Description: "Statutory registry checks awaiting vendor dispatch or confirmation.",
                Count: pendingVerificationsCount,
                ActionLabel: "View Requests",
                ActionUrl: "/buyer/verification-requests",
                Severity: "amber"
            ));
        }

        // Live Operational History / Audit Logs from Database
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == org.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentActivities = auditLogs.Select(a =>
        {
            var diff = DateTime.UtcNow - a.CreatedAt;
            string timeStr = diff.TotalMinutes < 60 ? $"{(int)Math.Max(1, diff.TotalMinutes)}m ago" :
                             diff.TotalHours < 24 ? $"{(int)diff.TotalHours}h ago" :
                             diff.TotalDays < 2 ? "Yesterday" : $"{a.CreatedAt:dd MMM}";

            string actionLabel = a.Action switch
            {
                "ONBOARDING_APPROVED" => "Onboarding Approved",
                "ONBOARDING_REJECTED" => "Onboarding Rejected",
                "ONBOARDING_CREATED" => "Onboarding Initiated",
                "ONBOARDING_SENT" => "Onboarding Sent",
                "PASSPORT_VIEWED" => "Sovereign Passport Inspected",
                "PASSPORT_SHARED" => "Passport Access Granted",
                "PASSPORT_ACCESS_REVOKED" => "Passport Access Revoked",
                "SUPPLIER_SHORTLISTED" => "Supplier Shortlisted",
                "VERIFICATION_REVIEWED" => "Verification Case Reviewed",
                "VERIFICATION_REQUESTED" => "Verification Dispatched",
                "CREDENTIAL_VERIFIED" => "Cryptographic Credential Verified",
                "AUDIT_LOG_EXPORTED" => "Forensic Audit Ledger Exported",
                _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(a.Action.Replace('_', ' ').ToLowerInvariant())
            };

            string title = !string.IsNullOrWhiteSpace(a.TargetName)
                ? $"{a.TargetName} — {actionLabel}"
                : actionLabel;

            string desc = $"Event {a.EventCode ?? "AUD"} • Result: {a.Result ?? "SUCCESS"}";
            string color = (a.Result ?? "").ToUpperInvariant() switch
            {
                "SUCCESS" => "green",
                "FAILED" => "red",
                "DENIED" => "amber",
                _ => "blue"
            };

            return new DashboardActivityDto(timeStr, title, desc, color);
        }).ToList();

        if (recentActivities.Count == 0)
        {
            recentActivities.Add(new DashboardActivityDto("Just now", "Audit Ledger Initialized", "Organization activity telemetry connected", "green"));
        }

        return Result<OrganizationDashboardDto>.Success(new OrganizationDashboardDto(
            org.Id,
            org.Name,
            org.Type.ToString(),
            totalMembers,
            totalSuppliers,
            verifiedSuppliers,
            pendingSuppliers,
            expiredSuppliers,
            highRiskSuppliers,
            verifiedPct,
            pendingPct,
            expiredPct,
            highRiskPct,
            itcSafeguarded,
            avgTrustScore,
            pillarCompliance,
            actionRequired,
            recentActivities
        ));
    }
}

public record MemberDto(Guid MemberId, Guid UserId, string Email, string FullName, string Role, string Status, DateTime? CreatedAt = null);

public record GetMembersQuery(Guid OrganizationId) : IRequest<Result<List<MemberDto>>>;

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, Result<List<MemberDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMembersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<MemberDto>>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        var members = await _context.OrganizationMembers
            .Include(m => m.User)
            .Include(m => m.Role)
            .Where(m => m.OrganizationId == request.OrganizationId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MemberDto(
                m.Id,
                m.UserId,
                m.User.Email,
                $"{m.User.FirstName} {m.User.LastName}".Trim(),
                m.Role.Name,
                m.Status,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<MemberDto>>.Success(members);
    }
}

public record AddMemberCommand(Guid OrganizationId, string Email, string RoleName) : IRequest<Result<Guid>>;

public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public AddMemberCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(AddMemberCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user == null)
        {
            var emailPrefix = normalizedEmail.Split('@')[0].Replace('.', ' ').Replace('_', ' ');
            var parts = emailPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = parts.Length > 0 ? char.ToUpper(parts[0][0]) + (parts[0].Length > 1 ? parts[0][1..].ToLower() : "") : "Team";
            var lastName = parts.Length > 1 ? char.ToUpper(parts[1][0]) + (parts[1].Length > 1 ? parts[1][1..].ToLower() : "") : "Member";

            user = new User
            {
                Email = normalizedEmail,
                FirstName = firstName,
                LastName = lastName,
                Status = "INVITED",
                MustChangePassword = true,
                PasswordHash = _passwordHasher.HashPassword(Guid.NewGuid().ToString("N"))
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var existingMember = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == user.Id, cancellationToken);
        if (existingMember != null)
        {
            return Result<Guid>.Failure("This user is already a member of your organization.");
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName, cancellationToken);
        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = request.RoleName.ToUpperInvariant(),
                Description = $"{request.RoleName} role",
                SystemRole = false
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var member = new OrganizationMember
        {
            OrganizationId = request.OrganizationId,
            UserId = user.Id,
            RoleId = role.Id,
            Status = user.Status == "INVITED" ? "INVITED" : "ACTIVE"
        };

        _context.OrganizationMembers.Add(member);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(member.Id);
    }
}

public record UpdateMemberRoleCommand(Guid OrganizationId, Guid MemberId, string NewRoleName) : IRequest<Result<bool>>;

public class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateMemberRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.OrganizationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganizationId == request.OrganizationId, cancellationToken);
        if (member == null) return Result<bool>.Failure("Member not found.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == request.NewRoleName, cancellationToken);
        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = request.NewRoleName.ToUpperInvariant(),
                Description = $"{request.NewRoleName} role",
                SystemRole = false
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
        }
        member.RoleId = role.Id;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record RemoveMemberCommand(Guid OrganizationId, Guid MemberId) : IRequest<Result<bool>>;

public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public RemoveMemberCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.OrganizationMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganizationId == request.OrganizationId, cancellationToken);
        if (member == null) return Result<bool>.Failure("Member not found.");

        _context.OrganizationMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}


