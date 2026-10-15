using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Admin;

// DTOs
public record AdminVerifierSummaryDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? Phone,
    string Status,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int AssignedCasesCount,
    int CompletedCasesCount,
    int PendingCasesCount);

public record AdminVerifierStatsDto(
    int TotalVerifiers,
    int ActiveVerifiers,
    int InactiveVerifiers,
    int SuspendedVerifiers);

public record AdminVerifiersPagedResponse(
    List<AdminVerifierSummaryDto> Items,
    AdminVerifierStatsDto Stats,
    int TotalCount,
    int Page,
    int PageSize);

public record VerifierAssignedCaseDto(
    Guid CaseId,
    string BusinessName,
    string VerificationType,
    string Status,
    string Priority,
    DateTime CreatedAt);

public record VerifierAuditEntryDto(
    Guid Id,
    string Action,
    string? Details,
    DateTime Timestamp);

public record AdminVerifierDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? Phone,
    string Status,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int TotalAssigned,
    int CompletedCases,
    int PendingCases,
    List<VerifierAssignedCaseDto> RecentCases,
    List<VerifierAuditEntryDto> AuditHistory);

// 1. Get Verifiers Query
public record GetAdminVerifiersQuery(
    string? Search = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<AdminVerifiersPagedResponse>>;

public class GetAdminVerifiersQueryHandler : IRequestHandler<GetAdminVerifiersQuery, Result<AdminVerifiersPagedResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminVerifiersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminVerifiersPagedResponse>> Handle(GetAdminVerifiersQuery request, CancellationToken cancellationToken)
    {
        var verifierRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        // Base query for all verifiers (not soft-deleted)
        var verifierUsersQuery = _context.Users
            .Where(u => !u.IsDeleted && u.Memberships.Any(m => m.RoleId == verifierRoleId || m.Role.Name == "VERIFIER"))
            .AsNoTracking();

        // Calculate platform-wide verifier stats
        var totalVerifiers = await verifierUsersQuery.CountAsync(cancellationToken);
        var activeVerifiers = await verifierUsersQuery.CountAsync(u => u.Status == "ACTIVE", cancellationToken);
        var inactiveVerifiers = await verifierUsersQuery.CountAsync(u => u.Status == "INACTIVE", cancellationToken);
        var suspendedVerifiers = await verifierUsersQuery.CountAsync(u => u.Status == "SUSPENDED", cancellationToken);

        var filteredQuery = verifierUsersQuery;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim().ToLowerInvariant();
            filteredQuery = filteredQuery.Where(u =>
                u.Email.ToLower().Contains(q) ||
                u.FirstName.ToLower().Contains(q) ||
                u.LastName.ToLower().Contains(q) ||
                (u.Phone != null && u.Phone.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var s = request.Status.Trim().ToUpperInvariant();
            filteredQuery = filteredQuery.Where(u => u.Status == s);
        }

        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var users = await filteredQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        // Aggregate verification cases assigned to these verifiers
        var cases = await _context.VerificationCases
            .Where(c => c.AssignedVerifierId.HasValue && userIds.Contains(c.AssignedVerifierId.Value))
            .Select(c => new { c.AssignedVerifierId, c.Status })
            .ToListAsync(cancellationToken);

        var items = users.Select(u =>
        {
            var userCases = cases.Where(c => c.AssignedVerifierId == u.Id).ToList();
            var assignedCount = userCases.Count;
            var completedCount = userCases.Count(c => c.Status == VerificationStatus.VERIFIED || c.Status == VerificationStatus.REJECTED);
            var pendingCount = userCases.Count(c => c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS);

            return new AdminVerifierSummaryDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Phone,
                u.Status,
                u.MustChangePassword,
                u.CreatedAt,
                u.LastLoginAt,
                assignedCount,
                completedCount,
                pendingCount
            );
        }).ToList();

        var stats = new AdminVerifierStatsDto(totalVerifiers, activeVerifiers, inactiveVerifiers, suspendedVerifiers);
        return Result<AdminVerifiersPagedResponse>.Success(new AdminVerifiersPagedResponse(items, stats, totalCount, page, pageSize));
    }
}

// 2. Get Verifier By Id Query
public record GetAdminVerifierByIdQuery(Guid Id) : IRequest<Result<AdminVerifierDetailDto>>;

public class GetAdminVerifierByIdQueryHandler : IRequestHandler<GetAdminVerifierByIdQuery, Result<AdminVerifierDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminVerifierByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminVerifierDetailDto>> Handle(GetAdminVerifierByIdQuery request, CancellationToken cancellationToken)
    {
        var verifierRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var user = await _context.Users
            .Include(u => u.Memberships)
                .ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted && u.Memberships.Any(m => m.RoleId == verifierRoleId || m.Role.Name == "VERIFIER"), cancellationToken);

        if (user == null)
            return Result<AdminVerifierDetailDto>.Failure("Verifier not found.");

        var cases = await _context.VerificationCases
            .Include(c => c.Business)
            .Where(c => c.AssignedVerifierId == user.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Take(15)
            .ToListAsync(cancellationToken);

        var totalAssigned = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id, cancellationToken);
        var completed = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id && (c.Status == VerificationStatus.VERIFIED || c.Status == VerificationStatus.REJECTED), cancellationToken);
        var pending = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id && (c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS), cancellationToken);

        var auditLogs = await _context.AuditLogs
            .Where(a => a.ResourceId == user.Id.ToString() || a.UserId == user.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new VerifierAuditEntryDto(
                a.Id,
                a.Action,
                a.NewValuesJson ?? a.ResourceType,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        var recentCasesDto = cases.Select(c => new VerifierAssignedCaseDto(
            c.Id,
            c.Business?.LegalName ?? "Unknown Entity",
            c.VerificationType,
            c.Status.ToString(),
            c.Priority,
            c.CreatedAt
        )).ToList();

        return Result<AdminVerifierDetailDto>.Success(new AdminVerifierDetailDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Phone,
            user.Status,
            user.MustChangePassword,
            user.CreatedAt,
            user.LastLoginAt,
            totalAssigned,
            completed,
            pending,
            recentCasesDto,
            auditLogs
        ));
    }
}

// 3. Create Verifier Command
public record CreateVerifierCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    bool MustChangePassword = true) : IRequest<Result<AdminVerifierSummaryDto>>;

public class CreateVerifierCommandValidator : AbstractValidator<CreateVerifierCommand>
{
    public CreateVerifierCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public class CreateVerifierCommandHandler : IRequestHandler<CreateVerifierCommand, Result<AdminVerifierSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public CreateVerifierCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AdminVerifierSummaryDto>> Handle(CreateVerifierCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
            return Result<AdminVerifierSummaryDto>.Failure("A user with this email address already exists.");

        var verifierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "VERIFIER", cancellationToken);
        if (verifierRole == null)
        {
            verifierRole = new Role
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "VERIFIER",
                Description = "Verification Operations Agent / Compliance Reviewer",
                SystemRole = true
            };
            _context.Roles.Add(verifierRole);
        }

        // Check for Internal Operations Organization
        var opsOrg = await _context.Organizations.FirstOrDefaultAsync(o => o.Name == "VeriPass Compliance Operations", cancellationToken);
        if (opsOrg == null)
        {
            opsOrg = new Organization
            {
                Name = "VeriPass Compliance Operations",
                Type = OrganizationType.BOTH,
                Email = "operations@veripass.in",
                IsActive = true
            };
            _context.Organizations.Add(opsOrg);
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Status = "ACTIVE",
            EmailVerified = true,
            MustChangePassword = request.MustChangePassword
        };
        _context.Users.Add(user);

        var membership = new OrganizationMember
        {
            OrganizationId = opsOrg.Id,
            UserId = user.Id,
            RoleId = verifierRole.Id,
            Status = "ACTIVE"
        };
        _context.OrganizationMembers.Add(membership);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            OrganizationId = opsOrg.Id,
            Action = "VERIFIER_CREATED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = $"{{\"email\":\"{user.Email}\",\"name\":\"{user.FirstName} {user.LastName}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AdminVerifierSummaryDto>.Success(new AdminVerifierSummaryDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Phone,
            user.Status,
            user.MustChangePassword,
            user.CreatedAt,
            user.LastLoginAt,
            0,
            0,
            0
        ));
    }
}

// 4. Update Verifier Command
public record UpdateVerifierCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone) : IRequest<Result<AdminVerifierSummaryDto>>;

public class UpdateVerifierCommandValidator : AbstractValidator<UpdateVerifierCommand>
{
    public UpdateVerifierCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public class UpdateVerifierCommandHandler : IRequestHandler<UpdateVerifierCommand, Result<AdminVerifierSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVerifierCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AdminVerifierSummaryDto>> Handle(UpdateVerifierCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<AdminVerifierSummaryDto>.Failure("Verifier not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Phone = request.Phone?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_UPDATED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = $"{{\"name\":\"{user.FirstName} {user.LastName}\",\"phone\":\"{user.Phone}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        var assigned = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id, cancellationToken);
        var completed = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id && (c.Status == VerificationStatus.VERIFIED || c.Status == VerificationStatus.REJECTED), cancellationToken);
        var pending = await _context.VerificationCases.CountAsync(c => c.AssignedVerifierId == user.Id && (c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS), cancellationToken);

        return Result<AdminVerifierSummaryDto>.Success(new AdminVerifierSummaryDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Phone,
            user.Status,
            user.MustChangePassword,
            user.CreatedAt,
            user.LastLoginAt,
            assigned,
            completed,
            pending
        ));
    }
}

// 5. Deactivate Verifier Command
public record DeactivateVerifierCommand(Guid Id, string? Reason = null) : IRequest<Result<bool>>;

public class DeactivateVerifierCommandHandler : IRequestHandler<DeactivateVerifierCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeactivateVerifierCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeactivateVerifierCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("Verifier not found.");

        user.Status = "INACTIVE";
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all active refresh tokens immediately
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_DEACTIVATED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = $"{{\"reason\":\"{request.Reason ?? "Administrative deactivation"}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 6. Reactivate Verifier Command
public record ReactivateVerifierCommand(Guid Id) : IRequest<Result<bool>>;

public class ReactivateVerifierCommandHandler : IRequestHandler<ReactivateVerifierCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReactivateVerifierCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(ReactivateVerifierCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("Verifier not found.");

        user.Status = "ACTIVE";
        user.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_REACTIVATED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = "{\"status\":\"ACTIVE\"}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 7. Suspend Verifier Command
public record SuspendVerifierCommand(Guid Id, string Reason) : IRequest<Result<bool>>;

public class SuspendVerifierCommandValidator : AbstractValidator<SuspendVerifierCommand>
{
    public SuspendVerifierCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("A suspension reason is required.");
    }
}

public class SuspendVerifierCommandHandler : IRequestHandler<SuspendVerifierCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SuspendVerifierCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(SuspendVerifierCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("Verifier not found.");

        user.Status = "SUSPENDED";
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all active refresh tokens immediately
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_SUSPENDED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = $"{{\"reason\":\"{request.Reason}\"}}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 8. Reset Verifier Password Command
public record ResetVerifierPasswordCommand(
    Guid Id,
    string NewPassword,
    bool MustChangePassword = true) : IRequest<Result<bool>>;

public class ResetVerifierPasswordCommandValidator : AbstractValidator<ResetVerifierPasswordCommand>
{
    public ResetVerifierPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long.");
    }
}

public class ResetVerifierPasswordCommandHandler : IRequestHandler<ResetVerifierPasswordCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public ResetVerifierPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(ResetVerifierPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("Verifier not found.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke active refresh tokens
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_PASSWORD_RESET",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = $"{{\"mustChangePassword\":{request.MustChangePassword.ToString().ToLower()}}}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 9. Delete Verifier Command (Soft-Delete)
public record DeleteVerifierCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteVerifierCommandHandler : IRequestHandler<DeleteVerifierCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteVerifierCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeleteVerifierCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("Verifier not found.");

        // Soft delete: sets IsDeleted = true, preserve foreign key references to verification cases, checks, evidence, audit logs
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.Status = "INACTIVE";
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke active refresh tokens
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? user.Id,
            Action = "VERIFIER_DELETED",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            NewValuesJson = "{\"deleted\":true,\"softDelete\":true}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
