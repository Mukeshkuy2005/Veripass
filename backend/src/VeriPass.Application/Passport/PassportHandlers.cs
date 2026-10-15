using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Passport;

// DTOs
public record BusinessPassportDto(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    string PassportNumber,
    string PublicIdentifier,
    string Version,
    string Status,
    int VerificationScore,
    DateTime? IssuedAt,
    DateTime? ExpiresAt,
    DateTime? LastVerifiedAt,
    string QrCodeUrl,
    List<PassportCredentialSummaryDto> VerifiedCredentials,
    List<PassportShareDto> ActiveShares);

public record PassportCredentialSummaryDto(
    string CredentialType,
    string CredentialName,
    string? DisplayValue,
    string VerificationStatus,
    DateTime? LastVerifiedAt);

public record PassportShareDto(
    Guid Id,
    string ShareToken,
    string? RecipientEmail,
    string? RecipientName,
    Dictionary<string, bool> AccessScope,
    DateTime? ExpiresAt,
    bool IsActive);

public record PublicPassportDto(
    string BusinessName,
    string PassportNumber,
    int VerificationScore,
    string Status,
    DateTime? LastVerifiedAt,
    Dictionary<string, object?> PublicData,
    List<string> VerifiedBadges);

// Queries
public record GetBusinessPassportQuery(Guid BusinessId) : IRequest<Result<BusinessPassportDto>>;

public class GetBusinessPassportQueryHandler : IRequestHandler<GetBusinessPassportQuery, Result<BusinessPassportDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessPassportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BusinessPassportDto>> Handle(GetBusinessPassportQuery request, CancellationToken cancellationToken)
    {
        var passport = await _context.BusinessPassports
            .Include(p => p.Business)
                .ThenInclude(b => b.Credentials)
                    .ThenInclude(c => c.CredentialType)
            .Include(p => p.Business)
                .ThenInclude(b => b.TrustScore)
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.BusinessId == request.BusinessId || p.Id == request.BusinessId || (p.Business != null && p.Business.OrganizationId == request.BusinessId), cancellationToken);

        if (passport == null)
        {
            var business = await _context.Businesses
                .Include(b => b.TrustScore)
                .FirstOrDefaultAsync(b => b.Id == request.BusinessId || b.OrganizationId == request.BusinessId, cancellationToken);

            if (business != null)
            {
                var randomSuffix = Random.Shared.Next(10000, 99999);
                var slugName = (business.LegalName ?? "business").ToLowerInvariant().Replace(" ", "-").Replace(".", "");
                var cleanSlug = System.Text.RegularExpressions.Regex.Replace(slugName, "[^a-z0-9-]", "");
                if (string.IsNullOrWhiteSpace(cleanSlug)) cleanSlug = "business";

                passport = new BusinessPassport
                {
                    BusinessId = business.Id,
                    PassportNumber = string.IsNullOrEmpty(business.BusinessIdNumber) ? $"VP-IN-{DateTime.UtcNow.Year}-{randomSuffix}" : business.BusinessIdNumber,
                    PublicIdentifier = $"{cleanSlug}-{randomSuffix}",
                    Version = "1.0",
                    Status = business.VerificationStatus == VerificationStatus.VERIFIED ? "ACTIVE" : "PENDING_VERIFICATION",
                    VerificationScore = business.TrustScore?.OverallScore ?? (business.VerificationStatus == VerificationStatus.VERIFIED ? 94 : 0),
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    LastVerifiedAt = DateTime.UtcNow,
                    IsPublic = true
                };
                _context.BusinessPassports.Add(passport);
                await _context.SaveChangesAsync(cancellationToken);

                passport = await _context.BusinessPassports
                    .Include(p => p.Business)
                        .ThenInclude(b => b.Credentials)
                            .ThenInclude(c => c.CredentialType)
                    .Include(p => p.Business)
                        .ThenInclude(b => b.TrustScore)
                    .Include(p => p.Shares)
                    .FirstOrDefaultAsync(p => p.Id == passport.Id, cancellationToken);
            }
        }

        if (passport == null)
            return Result<BusinessPassportDto>.Failure("Passport not generated for this business yet.");

        var credSummaries = passport.Business.Credentials.Select(c => new PassportCredentialSummaryDto(
            c.CredentialType.Code,
            c.CredentialType.Name,
            c.CredentialDisplayValue,
            c.VerificationStatus.ToString(),
            c.LastVerifiedAt
        )).ToList();

        var shares = passport.Shares.Where(s => s.IsActive).Select(s =>
        {
            var scope = new Dictionary<string, bool>();
            if (!string.IsNullOrEmpty(s.AccessScopeJson))
            {
                try { scope = JsonSerializer.Deserialize<Dictionary<string, bool>>(s.AccessScopeJson) ?? new(); } catch { }
            }
            return new PassportShareDto(s.Id, s.ShareTokenHash[..Math.Min(12, s.ShareTokenHash.Length)], s.RecipientEmail, s.RecipientName, scope, s.ExpiresAt, s.IsActive);
        }).ToList();

        var qrUrl = $"/api/v1/public/passports/{passport.PublicIdentifier}/qr";

        var dto = new BusinessPassportDto(
            passport.Id,
            passport.BusinessId,
            passport.Business.LegalName,
            passport.PassportNumber,
            passport.PublicIdentifier,
            passport.Version,
            passport.Status,
            passport.Business.TrustScore?.OverallScore ?? passport.VerificationScore,
            passport.IssuedAt,
            passport.ExpiresAt,
            passport.LastVerifiedAt,
            qrUrl,
            credSummaries,
            shares
        );

        return Result<BusinessPassportDto>.Success(dto);
    }
}

public record GetPublicPassportQuery(string Identifier, string? ShareToken = null) : IRequest<Result<PublicPassportDto>>;

public class GetPublicPassportQueryHandler : IRequestHandler<GetPublicPassportQuery, Result<PublicPassportDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicPassportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PublicPassportDto>> Handle(GetPublicPassportQuery request, CancellationToken cancellationToken)
    {
        var normId = (request.Identifier ?? "").Trim().ToLowerInvariant();
        var passport = await _context.BusinessPassports
            .Include(p => p.Business)
                .ThenInclude(b => b.Credentials)
                    .ThenInclude(c => c.CredentialType)
            .Include(p => p.Business)
                .ThenInclude(b => b.TrustScore)
            .Include(p => p.Business)
                .ThenInclude(b => b.Addresses)
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.PublicIdentifier.ToLower() == normId || p.PassportNumber.ToLower() == normId || p.Id.ToString() == normId, cancellationToken);

        if (passport == null)
            return Result<PublicPassportDto>.Failure("Public passport not found or invalid identifier.");

        // Record public access log
        _context.PassportAccessLogs.Add(new PassportAccessLog
        {
            PassportId = passport.Id,
            AccessedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        // Resolve scope: Default public scope vs ShareToken scope
        var allowBank = false;
        var allowPan = true;
        var allowGst = true;

        if (!string.IsNullOrEmpty(request.ShareToken))
        {
            var share = passport.Shares.FirstOrDefault(s => s.ShareTokenHash.StartsWith(request.ShareToken, StringComparison.OrdinalIgnoreCase));
            if (share != null && !string.IsNullOrEmpty(share.AccessScopeJson))
            {
                try
                {
                    var scope = JsonSerializer.Deserialize<Dictionary<string, bool>>(share.AccessScopeJson);
                    if (scope != null)
                    {
                        if (scope.TryGetValue("bank", out var b)) allowBank = b;
                        if (scope.TryGetValue("pan", out var p)) allowPan = p;
                        if (scope.TryGetValue("gst", out var g)) allowGst = g;
                    }
                }
                catch { }
            }
        }

        var publicData = new Dictionary<string, object?>
        {
            ["legal_name"] = passport.Business.LegalName,
            ["business_id"] = passport.Business.BusinessIdNumber,
            ["business_type"] = passport.Business.BusinessType,
            ["incorporation_year"] = passport.Business.IncorporationDate?.Year,
            ["city"] = passport.Business.Addresses.FirstOrDefault(a => a.IsPrimary)?.City,
            ["state"] = passport.Business.Addresses.FirstOrDefault(a => a.IsPrimary)?.State
        };

        if (allowGst && !string.IsNullOrEmpty(passport.Business.Gstin))
            publicData["gstin"] = passport.Business.Gstin;
        if (allowPan && !string.IsNullOrEmpty(passport.Business.Pan))
            publicData["pan_masked"] = $"******{passport.Business.Pan.TakeLast(4).ToArray()}";

        var badges = passport.Business.Credentials
            .Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
            .Select(c => c.CredentialType.Name)
            .ToList();

        var dto = new PublicPassportDto(
            passport.Business.LegalName,
            passport.PassportNumber,
            passport.Business.TrustScore?.OverallScore ?? 0,
            passport.Status,
            passport.LastVerifiedAt,
            publicData,
            badges
        );

        return Result<PublicPassportDto>.Success(dto);
    }
}

// Commands
public record GeneratePassportCommand(Guid BusinessId) : IRequest<Result<BusinessPassportDto>>;

public class GeneratePassportCommandHandler : IRequestHandler<GeneratePassportCommand, Result<BusinessPassportDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GeneratePassportCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<BusinessPassportDto>> Handle(GeneratePassportCommand request, CancellationToken cancellationToken)
    {
        var business = await _context.Businesses
            .Include(b => b.Passport)
            .Include(b => b.TrustScore)
            .FirstOrDefaultAsync(b => b.Id == request.BusinessId, cancellationToken);

        if (business == null)
            return Result<BusinessPassportDto>.Failure("Business not found.");

        if (business.Passport == null)
        {
            var randomSuffix = Random.Shared.Next(10000, 99999);
            var slugName = business.LegalName.ToLowerInvariant().Replace(" ", "-").Replace(".", "");
            var cleanSlug = System.Text.RegularExpressions.Regex.Replace(slugName, "[^a-z0-9-]", "");

            var passport = new BusinessPassport
            {
                BusinessId = business.Id,
                PassportNumber = $"VP-IN-{DateTime.UtcNow.Year}-{randomSuffix}",
                PublicIdentifier = $"{cleanSlug}-{randomSuffix}",
                Version = "1.0",
                Status = "ACTIVE",
                VerificationScore = business.TrustScore?.OverallScore ?? 0,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                LastVerifiedAt = DateTime.UtcNow,
                IsPublic = true
            };
            _context.BusinessPassports.Add(passport);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await _mediator.Send(new GetBusinessPassportQuery(business.Id), cancellationToken);
    }
}

public record CreatePassportShareCommand(
    Guid BusinessId,
    string? RecipientEmail,
    string? RecipientName,
    Dictionary<string, bool> AccessScope,
    int? ExpiryDays,
    Guid UserId) : IRequest<Result<string>>;

public class CreatePassportShareCommandHandler : IRequestHandler<CreatePassportShareCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public CreatePassportShareCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(CreatePassportShareCommand request, CancellationToken cancellationToken)
    {
        var passport = await _context.BusinessPassports
            .FirstOrDefaultAsync(p => p.BusinessId == request.BusinessId || p.Id == request.BusinessId, cancellationToken);

        if (passport == null)
            return Result<string>.Failure("Passport not generated yet.");

        var rawTokenBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(rawTokenBytes);
        var shareToken = Convert.ToHexString(rawTokenBytes);

        var share = new PassportShare
        {
            PassportId = passport.Id,
            SharedByUserId = request.UserId,
            ShareTokenHash = shareToken,
            RecipientEmail = request.RecipientEmail,
            RecipientName = request.RecipientName,
            AccessScopeJson = JsonSerializer.Serialize(request.AccessScope),
            ExpiresAt = request.ExpiryDays.HasValue ? DateTime.UtcNow.AddDays(request.ExpiryDays.Value) : null
        };

        _context.PassportShares.Add(share);
        await _context.SaveChangesAsync(cancellationToken);

        var shareUrl = $"/public/passport/{passport.PublicIdentifier}?token={shareToken[..16]}";
        return Result<string>.Success(shareUrl);
    }
}

public record PublishPassportCommand(Guid BusinessId) : IRequest<Result<bool>>;

public class PublishPassportCommandHandler : IRequestHandler<PublishPassportCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public PublishPassportCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(PublishPassportCommand request, CancellationToken cancellationToken)
    {
        var passport = await _context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == request.BusinessId || p.Id == request.BusinessId, cancellationToken);
        if (passport == null) return Result<bool>.Failure("Passport not found.");

        passport.IsPublic = true;
        passport.Status = "ACTIVE";
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record UnpublishPassportCommand(Guid BusinessId) : IRequest<Result<bool>>;

public class UnpublishPassportCommandHandler : IRequestHandler<UnpublishPassportCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UnpublishPassportCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UnpublishPassportCommand request, CancellationToken cancellationToken)
    {
        var passport = await _context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == request.BusinessId || p.Id == request.BusinessId, cancellationToken);
        if (passport == null) return Result<bool>.Failure("Passport not found.");

        passport.IsPublic = false;
        passport.Status = "UNPUBLISHED";
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record GetPassportSharesQuery(Guid PassportId) : IRequest<Result<List<PassportShareDto>>>;

public class GetPassportSharesQueryHandler : IRequestHandler<GetPassportSharesQuery, Result<List<PassportShareDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPassportSharesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PassportShareDto>>> Handle(GetPassportSharesQuery request, CancellationToken cancellationToken)
    {
        var shares = await _context.PassportShares
            .AsNoTracking()
            .Where(s => s.PassportId == request.PassportId || s.Passport.BusinessId == request.PassportId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = shares.Select(s =>
        {
            var scope = new Dictionary<string, bool>();
            if (!string.IsNullOrEmpty(s.AccessScopeJson))
            {
                try { scope = JsonSerializer.Deserialize<Dictionary<string, bool>>(s.AccessScopeJson) ?? new(); } catch { }
            }
            return new PassportShareDto(s.Id, s.ShareTokenHash[..Math.Min(12, s.ShareTokenHash.Length)], s.RecipientEmail, s.RecipientName, scope, s.ExpiresAt, s.IsActive);
        }).ToList();

        return Result<List<PassportShareDto>>.Success(dtos);
    }
}

public record DeletePassportShareCommand(Guid PassportId, Guid ShareId) : IRequest<Result<bool>>;

public class DeletePassportShareCommandHandler : IRequestHandler<DeletePassportShareCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeletePassportShareCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeletePassportShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _context.PassportShares.FirstOrDefaultAsync(s => s.Id == request.ShareId, cancellationToken);
        if (share == null) return Result<bool>.Failure("Passport share not found.");

        _context.PassportShares.Remove(share);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record PublicPassportVerificationDto(
    bool IsAuthentic,
    string BusinessName,
    string PassportNumber,
    string PublicIdentifier,
    string Status,
    int TrustScore,
    DateTime? IssuedAt,
    DateTime? ExpiresAt,
    List<string> VerifiedBadges);

public record VerifyPublicPassportQuery(string Identifier) : IRequest<Result<PublicPassportVerificationDto>>;

public class VerifyPublicPassportQueryHandler : IRequestHandler<VerifyPublicPassportQuery, Result<PublicPassportVerificationDto>>
{
    private readonly IApplicationDbContext _context;

    public VerifyPublicPassportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PublicPassportVerificationDto>> Handle(VerifyPublicPassportQuery request, CancellationToken cancellationToken)
    {
        var normId = (request.Identifier ?? "").Trim().ToLowerInvariant();
        var passport = await _context.BusinessPassports
            .Include(p => p.Business)
                .ThenInclude(b => b.Credentials)
                    .ThenInclude(c => c.CredentialType)
            .Include(p => p.Business)
                .ThenInclude(b => b.TrustScore)
            .FirstOrDefaultAsync(p => p.PublicIdentifier.ToLower() == normId || p.PassportNumber.ToLower() == normId || p.Id.ToString() == normId, cancellationToken);

        if (passport == null)
            return Result<PublicPassportVerificationDto>.Failure("Passport not found.");

        var badges = passport.Business.Credentials
            .Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
            .Select(c => c.CredentialType.Name)
            .ToList();

        var isAuthentic = passport.Status == "ACTIVE" && (passport.ExpiresAt == null || passport.ExpiresAt > DateTime.UtcNow);

        return Result<PublicPassportVerificationDto>.Success(new PublicPassportVerificationDto(
            isAuthentic,
            passport.Business.LegalName,
            passport.PassportNumber,
            passport.PublicIdentifier,
            passport.Status,
            passport.Business.TrustScore?.OverallScore ?? 0,
            passport.IssuedAt,
            passport.ExpiresAt,
            badges
        ));
    }
}
