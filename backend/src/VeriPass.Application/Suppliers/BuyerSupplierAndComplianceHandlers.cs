using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Suppliers;

// DTOs
public record SupplierSearchResultDto(
    Guid BusinessId,
    string LegalName,
    string? TradeName,
    string BusinessIdNumber,
    string? City,
    string? State,
    string? Gstin,
    int TrustScore,
    string VerificationStatus,
    List<string> VerifiedCredentials,
    bool HasActiveGst,
    bool IsMsmRegistered,
    string? BusinessType = null,
    string? Category = null,
    int? ComplianceScore = null,
    int? YearsActive = null,
    string? Pan = null,
    string? Cin = null,
    string? UdyamNumber = null,
    string? RiskLevel = null,
    bool BuyerEligible = false);

public record SupplierCompareDto(
    Guid BusinessId,
    string LegalName,
    string BusinessIdNumber,
    int OverallTrustScore,
    int IdentityScore,
    int CredentialScore,
    int ComplianceScore,
    int VerificationScore,
    int RiskScore,
    string? Gstin,
    string? Pan,
    string? Udyam,
    int? EmployeeCount,
    List<string> VerifiedCredentials,
    string? TradeName = null,
    string? Category = null,
    string? BusinessType = null,
    string? Location = null,
    string? State = null,
    int? YearsActive = null,
    string? VerificationStatus = null,
    string? RiskLevel = null,
    bool BuyerEligible = false);

public record ShortlistDto(
    Guid Id,
    string Name,
    string? Description,
    int SupplierCount,
    List<ShortlistItemDto> Items);

public record ShortlistItemDto(
    Guid BusinessId,
    string LegalName,
    string BusinessIdNumber,
    int TrustScore,
    string? Notes,
    DateTime AddedAt,
    string? TradeName = null,
    string? Category = null,
    string? Industry = null,
    string? Location = null,
    string? State = null,
    int? ComplianceScore = null,
    string? RiskLevel = null,
    string? VerificationStatus = null,
    string? Gstin = null,
    List<string>? Certifications = null);

public record ComplianceEventDto(
    Guid Id,
    Guid BusinessId,
    string EventType,
    string Severity,
    string Title,
    string Description,
    DateTime? DueDate,
    bool IsAcknowledged,
    DateTime CreatedAt);

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? ResourceType,
    string? ResourceId,
    bool IsRead,
    DateTime CreatedAt);

// 1. Supplier Search Query
public record SearchSuppliersQuery(
    string? Query = null,
    string? State = null,
    string? Industry = null,
    int? MinTrustScore = null,
    string? RequiredCredential = null,
    string? VerificationStatus = null,
    int Page = 1,
    int PageSize = 20,
    bool BuyerOnly = false) : IRequest<Result<List<SupplierSearchResultDto>>>;

public class SearchSuppliersQueryHandler : IRequestHandler<SearchSuppliersQuery, Result<List<SupplierSearchResultDto>>>
{
    private readonly IApplicationDbContext _context;

    public SearchSuppliersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SupplierSearchResultDto>>> Handle(SearchSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Businesses
            .Include(b => b.Addresses)
            .Include(b => b.TrustScore)
            .Include(b => b.Credentials)
                .ThenInclude(c => c.CredentialType)
            .Include(b => b.Organization)
            .Where(b => b.Organization == null || b.Organization.Type == OrganizationType.SUPPLIER)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var q = request.Query.Trim().ToLowerInvariant();
            query = query.Where(b => b.LegalName.ToLower().Contains(q) || (b.TradeName != null && b.TradeName.ToLower().Contains(q)) || (b.Gstin != null && b.Gstin.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(request.Industry))
        {
            var ind = request.Industry.Trim().ToLowerInvariant();
            query = query.Where(b => (b.BusinessType != null && b.BusinessType.ToLower().Contains(ind)) || (b.Description != null && b.Description.ToLower().Contains(ind)));
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            query = query.Where(b => b.Addresses.Any(a => a.State.ToLower().Contains(request.State.ToLower())));
        }

        if (request.MinTrustScore.HasValue)
        {
            query = query.Where(b => b.TrustScore != null && b.TrustScore.OverallScore >= request.MinTrustScore.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.RequiredCredential))
        {
            query = query.Where(b => b.Credentials.Any(c => c.CredentialType.Code == request.RequiredCredential && c.VerificationStatus == VerificationStatus.VERIFIED));
        }

        if (!string.IsNullOrWhiteSpace(request.VerificationStatus) && Enum.TryParse<VerificationStatus>(request.VerificationStatus, true, out var vs))
        {
            query = query.Where(b => b.VerificationStatus == vs);
        }

        // Authoritative Buyer Supplier Selection Rule: Only suppliers with score > 50 are eligible to appear
        if (request.BuyerOnly)
        {
            query = query.Where(b => b.TrustScore != null && b.TrustScore.OverallScore > 50);
        }

        var results = await query
            .OrderByDescending(b => b.TrustScore != null ? b.TrustScore.OverallScore : 0)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = results.Select(b =>
        {
            var primaryAddr = b.Addresses.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses.FirstOrDefault();
            var verifiedCreds = b.Credentials
                .Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
                .Select(c => c.CredentialType.Code)
                .ToList();

            var years = b.IncorporationDate.HasValue 
                ? Math.Max(1, (int)((DateTime.UtcNow - b.IncorporationDate.Value).TotalDays / 365.25))
                : 0;

            var score = b.TrustScore?.OverallScore ?? 0;
            var riskLevel = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
            var buyerEligible = SupplierRiskPolicy.IsEligibleForBuyer(score);

            return new SupplierSearchResultDto(
                b.Id,
                b.LegalName,
                b.TradeName,
                b.BusinessIdNumber,
                primaryAddr?.City ?? "",
                primaryAddr?.State ?? "",
                b.Gstin,
                score,
                b.VerificationStatus.ToString(),
                verifiedCreds,
                !string.IsNullOrEmpty(b.Gstin),
                !string.IsNullOrEmpty(b.UdyamNumber),
                b.BusinessType ?? "",
                b.Description ?? "",
                b.TrustScore?.ComplianceScore ?? 0,
                years,
                b.Pan,
                b.Cin,
                b.UdyamNumber,
                riskLevel,
                buyerEligible
            );
        }).ToList();

        return Result<List<SupplierSearchResultDto>>.Success(dtos);
    }
}

public record GetSupplierByIdQuery(Guid BusinessId) : IRequest<Result<SupplierSearchResultDto>>;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, Result<SupplierSearchResultDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSupplierByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SupplierSearchResultDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses
            .Include(b => b.Addresses)
            .Include(b => b.TrustScore)
            .Include(b => b.Credentials)
                .ThenInclude(c => c.CredentialType)
            .FirstOrDefaultAsync(b => b.Id == request.BusinessId, cancellationToken);

        if (b == null) return Result<SupplierSearchResultDto>.Failure("Supplier not found.");

        var primaryAddr = b.Addresses.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses.FirstOrDefault();
        var verifiedCreds = b.Credentials
            .Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
            .Select(c => c.CredentialType.Code)
            .ToList();

        var years = b.IncorporationDate.HasValue 
            ? Math.Max(1, (int)((DateTime.UtcNow - b.IncorporationDate.Value).TotalDays / 365.25))
            : 0;

        var score = b.TrustScore?.OverallScore ?? 0;
        var riskLevel = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
        var buyerEligible = SupplierRiskPolicy.IsEligibleForBuyer(score);

        var dto = new SupplierSearchResultDto(
            b.Id,
            b.LegalName,
            b.TradeName,
            b.BusinessIdNumber,
            primaryAddr?.City ?? "",
            primaryAddr?.State ?? "",
            b.Gstin,
            score,
            b.VerificationStatus.ToString(),
            verifiedCreds,
            !string.IsNullOrEmpty(b.Gstin),
            !string.IsNullOrEmpty(b.UdyamNumber),
            b.BusinessType ?? "",
            b.Description ?? "",
            b.TrustScore?.ComplianceScore ?? 0,
            years,
            b.Pan,
            b.Cin,
            b.UdyamNumber,
            riskLevel,
            buyerEligible
        );

        return Result<SupplierSearchResultDto>.Success(dto);
    }
}

// 2. Compare Suppliers Query
public record CompareSuppliersQuery(List<Guid> BusinessIds) : IRequest<Result<List<SupplierCompareDto>>>;

public class CompareSuppliersQueryHandler : IRequestHandler<CompareSuppliersQuery, Result<List<SupplierCompareDto>>>
{
    private readonly IApplicationDbContext _context;

    public CompareSuppliersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SupplierCompareDto>>> Handle(CompareSuppliersQuery request, CancellationToken cancellationToken)
    {
        var businesses = await _context.Businesses
            .Include(b => b.Addresses)
            .Include(b => b.TrustScore)
            .Include(b => b.Credentials)
                .ThenInclude(c => c.CredentialType)
            .Where(b => request.BusinessIds.Contains(b.Id))
            .ToListAsync(cancellationToken);

        var dtos = businesses.Select(b =>
        {
            var primaryAddr = b.Addresses.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses.FirstOrDefault();
            var years = b.IncorporationDate.HasValue
                ? Math.Max(1, (int)((DateTime.UtcNow - b.IncorporationDate.Value).TotalDays / 365.25))
                : 0;

            var loc = primaryAddr != null
                ? (string.IsNullOrEmpty(primaryAddr.City) ? (primaryAddr.State ?? "") : $"{primaryAddr.City}, {primaryAddr.State}")
                : "";

            var score = b.TrustScore?.OverallScore ?? 0;
            var riskLevel = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
            var buyerEligible = SupplierRiskPolicy.IsEligibleForBuyer(score);

            return new SupplierCompareDto(
                b.Id,
                b.LegalName,
                b.BusinessIdNumber,
                score,
                b.TrustScore?.IdentityScore ?? 0,
                b.TrustScore?.CredentialScore ?? 0,
                b.TrustScore?.ComplianceScore ?? 0,
                b.TrustScore?.VerificationScore ?? 0,
                b.TrustScore?.RiskScore ?? 0,
                b.Gstin,
                b.Pan,
                b.UdyamNumber,
                b.EmployeeCount,
                b.Credentials.Where(c => c.VerificationStatus == VerificationStatus.VERIFIED).Select(c => c.CredentialType.Name).ToList(),
                b.TradeName ?? b.LegalName,
                b.Description ?? "",
                b.BusinessType ?? "",
                loc,
                primaryAddr?.State ?? "",
                years,
                b.VerificationStatus.ToString(),
                riskLevel,
                buyerEligible
            );
        }).ToList();

        return Result<List<SupplierCompareDto>>.Success(dtos);
    }
}

// 3. Buyer Shortlists
public record GetShortlistsQuery(Guid BuyerOrganizationId) : IRequest<Result<List<ShortlistDto>>>;

public class GetShortlistsQueryHandler : IRequestHandler<GetShortlistsQuery, Result<List<ShortlistDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetShortlistsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ShortlistDto>>> Handle(GetShortlistsQuery request, CancellationToken cancellationToken)
    {
        var lists = await _context.SupplierShortlists
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.TrustScore)
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.Addresses)
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.Credentials)
                        .ThenInclude(c => c.CredentialType)
            .Where(s => s.BuyerOrganizationId == request.BuyerOrganizationId)
            .ToListAsync(cancellationToken);

        var dtos = lists.Select(l => new ShortlistDto(
            l.Id,
            l.Name,
            l.Description,
            l.Items.Count,
            l.Items.Select(i => {
                var b = i.Business;
                var primaryAddr = b.Addresses?.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses?.FirstOrDefault();
                var loc = primaryAddr != null
                    ? (string.IsNullOrEmpty(primaryAddr.City) ? (primaryAddr.State ?? "") : $"{primaryAddr.City}, {primaryAddr.State}")
                    : "";
                var score = b.TrustScore?.OverallScore ?? 0;
                var comp = b.TrustScore?.ComplianceScore ?? 0;
                var risk = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
                var creds = b.Credentials?.Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
                    .Select(c => c.CredentialType.Name).ToList() ?? new List<string>();

                return new ShortlistItemDto(
                    i.BusinessId,
                    b.LegalName,
                    b.BusinessIdNumber,
                    score,
                    i.Notes,
                    i.CreatedAt,
                    b.TradeName ?? b.LegalName,
                    b.Description ?? "",
                    b.BusinessType ?? "",
                    loc,
                    primaryAddr?.State ?? "",
                    comp,
                    risk,
                    b.VerificationStatus.ToString(),
                    b.Gstin,
                    creds
                );
            }).ToList()
        )).ToList();

        return Result<List<ShortlistDto>>.Success(dtos);
    }
}

public record AddToShortlistCommand(Guid ShortlistId, Guid BusinessId, string? Notes) : IRequest<Result<bool>>;

public class AddToShortlistCommandHandler : IRequestHandler<AddToShortlistCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public AddToShortlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(AddToShortlistCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.SupplierShortlistItems
            .AnyAsync(i => i.ShortlistId == request.ShortlistId && i.BusinessId == request.BusinessId, cancellationToken);

        if (existing)
            return Result<bool>.Success(true);

        _context.SupplierShortlistItems.Add(new SupplierShortlistItem
        {
            ShortlistId = request.ShortlistId,
            BusinessId = request.BusinessId,
            Notes = request.Notes
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record CreateShortlistCommand(Guid BuyerOrganizationId, string Name, string? Description) : IRequest<Result<Guid>>;

public class CreateShortlistCommandHandler : IRequestHandler<CreateShortlistCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateShortlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateShortlistCommand request, CancellationToken cancellationToken)
    {
        var list = new SupplierShortlist
        {
            BuyerOrganizationId = request.BuyerOrganizationId,
            Name = request.Name,
            Description = request.Description
        };

        _context.SupplierShortlists.Add(list);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(list.Id);
    }
}

public record GetShortlistByIdQuery(Guid ShortlistId) : IRequest<Result<ShortlistDto>>;

public class GetShortlistByIdQueryHandler : IRequestHandler<GetShortlistByIdQuery, Result<ShortlistDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShortlistByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ShortlistDto>> Handle(GetShortlistByIdQuery request, CancellationToken cancellationToken)
    {
        var l = await _context.SupplierShortlists
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.TrustScore)
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.Addresses)
            .Include(s => s.Items)
                .ThenInclude(i => i.Business)
                    .ThenInclude(b => b.Credentials)
                        .ThenInclude(c => c.CredentialType)
            .FirstOrDefaultAsync(s => s.Id == request.ShortlistId, cancellationToken);

        if (l == null) return Result<ShortlistDto>.Failure("Shortlist not found.");

        var dto = new ShortlistDto(
            l.Id,
            l.Name,
            l.Description,
            l.Items.Count,
            l.Items.Select(i => {
                var b = i.Business;
                var primaryAddr = b.Addresses?.FirstOrDefault(a => a.IsPrimary) ?? b.Addresses?.FirstOrDefault();
                var loc = primaryAddr != null
                    ? (string.IsNullOrEmpty(primaryAddr.City) ? (primaryAddr.State ?? "") : $"{primaryAddr.City}, {primaryAddr.State}")
                    : "";
                var score = b.TrustScore?.OverallScore ?? 0;
                var comp = b.TrustScore?.ComplianceScore ?? 0;
                var risk = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
                var creds = b.Credentials?.Where(c => c.VerificationStatus == VerificationStatus.VERIFIED)
                    .Select(c => c.CredentialType.Name).ToList() ?? new List<string>();

                return new ShortlistItemDto(
                    i.BusinessId,
                    b.LegalName,
                    b.BusinessIdNumber,
                    score,
                    i.Notes,
                    i.CreatedAt,
                    b.TradeName ?? b.LegalName,
                    b.Description ?? "",
                    b.BusinessType ?? "",
                    loc,
                    primaryAddr?.State ?? "",
                    comp,
                    risk,
                    b.VerificationStatus.ToString(),
                    b.Gstin,
                    creds
                );
            }).ToList()
        );

        return Result<ShortlistDto>.Success(dto);
    }
}

public record RemoveFromShortlistCommand(Guid ShortlistId, Guid BusinessId) : IRequest<Result<bool>>;

public class RemoveFromShortlistCommandHandler : IRequestHandler<RemoveFromShortlistCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public RemoveFromShortlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RemoveFromShortlistCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.SupplierShortlistItems
            .FirstOrDefaultAsync(i => i.ShortlistId == request.ShortlistId && i.BusinessId == request.BusinessId, cancellationToken);

        if (item == null) return Result<bool>.Failure("Item not found in shortlist.");

        _context.SupplierShortlistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record DeleteShortlistCommand(Guid ShortlistId, Guid BuyerOrganizationId) : IRequest<Result<bool>>;

public class DeleteShortlistCommandHandler : IRequestHandler<DeleteShortlistCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteShortlistCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteShortlistCommand request, CancellationToken cancellationToken)
    {
        var list = await _context.SupplierShortlists
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.ShortlistId && s.BuyerOrganizationId == request.BuyerOrganizationId, cancellationToken);

        if (list == null) return Result<bool>.Failure("Shortlist not found.");

        _context.SupplierShortlists.Remove(list);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 4. Compliance Dashboard & Events
public record GetComplianceEventsQuery(Guid BusinessId) : IRequest<Result<List<ComplianceEventDto>>>;

public class GetComplianceEventsQueryHandler : IRequestHandler<GetComplianceEventsQuery, Result<List<ComplianceEventDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetComplianceEventsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ComplianceEventDto>>> Handle(GetComplianceEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.ComplianceEvents
            .Where(e => e.BusinessId == request.BusinessId || e.Business.OrganizationId == request.BusinessId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = events.Select(e => new ComplianceEventDto(
            e.Id,
            e.BusinessId,
            e.EventType,
            e.Severity.ToString(),
            e.Title,
            e.Description,
            e.DueDate,
            e.IsAcknowledged,
            e.CreatedAt
        )).ToList();

        return Result<List<ComplianceEventDto>>.Success(dtos);
    }
}

public record AcknowledgeComplianceEventCommand(Guid EventId) : IRequest<Result<bool>>;

public class AcknowledgeComplianceEventCommandHandler : IRequestHandler<AcknowledgeComplianceEventCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public AcknowledgeComplianceEventCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(AcknowledgeComplianceEventCommand request, CancellationToken cancellationToken)
    {
        var ev = await _context.ComplianceEvents.FindAsync(new object[] { request.EventId }, cancellationToken);
        if (ev == null) return Result<bool>.Failure("Event not found.");

        ev.IsAcknowledged = true;
        ev.AcknowledgedAt = DateTime.UtcNow;
        ev.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// 5. Notifications
public record GetNotificationsQuery(Guid UserId) : IRequest<Result<List<NotificationDto>>>;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, Result<List<NotificationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifs = await _context.Notifications
            .Where(n => n.UserId == request.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var dtos = notifs.Select(n => new NotificationDto(
            n.Id,
            n.Type.ToString(),
            n.Title,
            n.Message,
            n.ResourceType,
            n.ResourceId,
            n.IsRead,
            n.CreatedAt
        )).ToList();

        return Result<List<NotificationDto>>.Success(dtos);
    }
}

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result<bool>>;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var n = await _context.Notifications.FindAsync(new object[] { request.NotificationId }, cancellationToken);
        if (n != null)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Result<bool>.Success(true);
    }
}
