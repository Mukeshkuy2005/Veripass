using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Businesses;

// DTOs
public record BusinessProfileDto(
    Guid Id,
    Guid OrganizationId,
    string BusinessIdNumber,
    string LegalName,
    string? TradeName,
    string? BusinessType,
    DateTime? IncorporationDate,
    string? Pan,
    string? Gstin,
    string? Cin,
    string? UdyamNumber,
    string? Website,
    int? EmployeeCount,
    string? Description,
    string VerificationStatus,
    int TrustScore,
    List<AddressDto> Addresses,
    List<DirectorDto> Directors,
    List<BankAccountDto> BankAccounts);

public record AddressDto(
    Guid Id,
    string AddressType,
    string Line1,
    string? Line2,
    string City,
    string? District,
    string State,
    string Pincode,
    string Country,
    bool IsPrimary,
    string VerificationStatus);

public record DirectorDto(
    Guid Id,
    string FullName,
    string Designation,
    string? Din,
    string? PanMasked,
    DateTime? AppointmentDate,
    string Status);

public record BankAccountDto(
    Guid Id,
    string AccountHolderName,
    string BankName,
    string AccountNumberLast4,
    string Ifsc,
    string AccountType,
    string VerificationStatus,
    DateTime? VerifiedAt,
    bool IsPrimary = false);

public record BusinessDashboardDto(
    Guid BusinessId,
    string BusinessName,
    string BusinessIdNumber,
    int OverallTrustScore,
    int IdentityScore,
    int CredentialScore,
    int ComplianceScore,
    int VerificationScore,
    int RiskScore,
    string VerificationStatus,
    int TotalCredentials,
    int VerifiedCredentials,
    int PendingVerificationCount,
    int ActiveAlertsCount,
    int ConnectedBuyersCount,
    List<RecentActivityDto> RecentActivities);

public record RecentActivityDto(
    string Title,
    string Description,
    DateTime Timestamp,
    string Type);

// Queries
public record GetBusinessProfileQuery(Guid BusinessId) : IRequest<Result<BusinessProfileDto>>;

public class GetBusinessProfileQueryHandler : IRequestHandler<GetBusinessProfileQuery, Result<BusinessProfileDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BusinessProfileDto>> Handle(GetBusinessProfileQuery request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses
            .Include(x => x.Addresses)
            .Include(x => x.Directors)
            .Include(x => x.BankAccounts)
            .Include(x => x.TrustScore)
            .FirstOrDefaultAsync(x => x.Id == request.BusinessId || x.OrganizationId == request.BusinessId, cancellationToken);

        if (b == null)
            return Result<BusinessProfileDto>.Failure("Business entity not found.");

        var dto = new BusinessProfileDto(
            b.Id,
            b.OrganizationId,
            b.BusinessIdNumber,
            b.LegalName,
            b.TradeName,
            b.BusinessType,
            b.IncorporationDate,
            b.Pan,
            b.Gstin,
            b.Cin,
            b.UdyamNumber,
            b.Website,
            b.EmployeeCount,
            b.Description,
            b.VerificationStatus.ToString(),
            b.TrustScore?.OverallScore ?? 0,
            b.Addresses.Select(a => new AddressDto(a.Id, a.AddressType, a.Line1, a.Line2, a.City, a.District, a.State, a.Pincode, a.Country, a.IsPrimary, a.VerificationStatus.ToString())).ToList(),
            b.Directors.Select(d => new DirectorDto(d.Id, d.FullName, d.Designation, d.Din, d.PanMasked, d.AppointmentDate, d.Status)).ToList(),
            b.BankAccounts.Select(acc => new BankAccountDto(acc.Id, acc.AccountHolderName, acc.BankName, acc.AccountNumberLast4, acc.Ifsc, acc.AccountType, acc.VerificationStatus.ToString(), acc.VerifiedAt)).ToList()
        );

        return Result<BusinessProfileDto>.Success(dto);
    }
}

public record GetBusinessDashboardQuery(Guid BusinessId) : IRequest<Result<BusinessDashboardDto>>;

public class GetBusinessDashboardQueryHandler : IRequestHandler<GetBusinessDashboardQuery, Result<BusinessDashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BusinessDashboardDto>> Handle(GetBusinessDashboardQuery request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses
            .Include(x => x.TrustScore)
            .Include(x => x.Credentials)
            .Include(x => x.ComplianceEvents)
            .Include(x => x.SupplierRelationships)
            .FirstOrDefaultAsync(x => x.Id == request.BusinessId || x.OrganizationId == request.BusinessId, cancellationToken);

        if (b == null)
            return Result<BusinessDashboardDto>.Failure("Business not found.");

        var totalCreds = b.Credentials.Count;
        var verifiedCreds = b.Credentials.Count(c => c.VerificationStatus == VerificationStatus.VERIFIED);
        var pendingCases = await _context.VerificationCases
            .CountAsync(c => (c.BusinessId == b.Id) && c.Status == VerificationStatus.PENDING, cancellationToken);
        var activeAlerts = b.ComplianceEvents.Count(e => e.ResolvedAt == null);
        var connectedBuyers = b.SupplierRelationships.Count(r => r.Status == RelationshipStatus.APPROVED);

        var activities = await _context.AuditLogs
            .Where(a => a.OrganizationId == b.OrganizationId || a.ResourceId == b.Id.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new RecentActivityDto(a.Action, a.ResourceType, a.CreatedAt, "AUDIT"))
            .ToListAsync(cancellationToken);

        var dto = new BusinessDashboardDto(
            b.Id,
            b.LegalName,
            b.BusinessIdNumber,
            b.TrustScore?.OverallScore ?? 0,
            b.TrustScore?.IdentityScore ?? 0,
            b.TrustScore?.CredentialScore ?? 0,
            b.TrustScore?.ComplianceScore ?? 0,
            b.TrustScore?.VerificationScore ?? 0,
            b.TrustScore?.RiskScore ?? 100,
            b.VerificationStatus.ToString(),
            totalCreds,
            verifiedCreds,
            pendingCases,
            activeAlerts,
            connectedBuyers,
            activities
        );

        return Result<BusinessDashboardDto>.Success(dto);
    }
}

// Commands
public record UpdateBusinessAddressDto(
    string? AddressType,
    string? Line1,
    string? Line2,
    string? City,
    string? District,
    string? State,
    string? Pincode,
    string? Country,
    bool? IsPrimary);

public record UpdateBusinessCommand(
    Guid BusinessId,
    string LegalName,
    string? TradeName,
    string? BusinessType,
    string? Pan,
    string? Gstin,
    string? Cin,
    string? UdyamNumber,
    string? Website,
    int? EmployeeCount,
    string? Description,
    List<UpdateBusinessAddressDto>? Addresses = null) : IRequest<Result<bool>>;

public class UpdateBusinessCommandHandler : IRequestHandler<UpdateBusinessCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITrustScoreCalculator _trustScoreCalculator;

    public UpdateBusinessCommandHandler(IApplicationDbContext context, ITrustScoreCalculator trustScoreCalculator)
    {
        _context = context;
        _trustScoreCalculator = trustScoreCalculator;
    }

    public async Task<Result<bool>> Handle(UpdateBusinessCommand request, CancellationToken cancellationToken)
    {
        var b = await _context.Businesses.FindAsync(new object[] { request.BusinessId }, cancellationToken);
        if (b == null) return Result<bool>.Failure("Business not found.");

        b.LegalName = request.LegalName.Trim();
        b.TradeName = request.TradeName?.Trim();
        b.BusinessType = request.BusinessType?.Trim();
        b.Pan = request.Pan?.Trim().ToUpperInvariant();
        b.Gstin = request.Gstin?.Trim().ToUpperInvariant();
        b.Cin = request.Cin?.Trim().ToUpperInvariant();
        b.UdyamNumber = request.UdyamNumber?.Trim().ToUpperInvariant();
        b.Website = request.Website?.Trim();
        b.EmployeeCount = request.EmployeeCount;
        b.Description = request.Description;

        // Keep parent Organization name in sync with updated legal name
        var org = await _context.Organizations.FindAsync(new object[] { b.OrganizationId }, cancellationToken);
        if (org != null && !string.IsNullOrWhiteSpace(b.LegalName))
        {
            org.Name = b.LegalName;
        }

        // Synchronize primary address if provided
        if (request.Addresses != null && request.Addresses.Count > 0)
        {
            var primaryReq = request.Addresses.FirstOrDefault(a => a.IsPrimary == true) ?? request.Addresses[0];
            if (!string.IsNullOrWhiteSpace(primaryReq.Line1) || !string.IsNullOrWhiteSpace(primaryReq.City))
            {
                var existingAddresses = await _context.BusinessAddresses
                    .Where(a => a.BusinessId == b.Id)
                    .ToListAsync(cancellationToken);
                var primaryAddr = existingAddresses.FirstOrDefault(a => a.IsPrimary) ?? existingAddresses.FirstOrDefault();
                if (primaryAddr != null)
                {
                    primaryAddr.Line1 = primaryReq.Line1 ?? primaryAddr.Line1;
                    primaryAddr.Line2 = primaryReq.Line2 ?? primaryAddr.Line2;
                    primaryAddr.City = primaryReq.City ?? primaryAddr.City;
                    primaryAddr.District = primaryReq.District ?? primaryAddr.District;
                    primaryAddr.State = primaryReq.State ?? primaryAddr.State;
                    primaryAddr.Pincode = primaryReq.Pincode ?? primaryAddr.Pincode;
                    primaryAddr.Country = primaryReq.Country ?? primaryAddr.Country ?? "India";
                    primaryAddr.AddressType = primaryReq.AddressType ?? primaryAddr.AddressType ?? "REGISTERED";
                    primaryAddr.IsPrimary = true;
                    primaryAddr.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.BusinessAddresses.Add(new BusinessAddress
                    {
                        BusinessId = b.Id,
                        Line1 = primaryReq.Line1 ?? "",
                        Line2 = primaryReq.Line2,
                        City = primaryReq.City ?? "",
                        District = primaryReq.District,
                        State = primaryReq.State ?? "",
                        Pincode = primaryReq.Pincode ?? "",
                        Country = primaryReq.Country ?? "India",
                        AddressType = primaryReq.AddressType ?? "REGISTERED",
                        IsPrimary = true,
                        VerificationStatus = VerificationStatus.VERIFIED
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Recalculate score after updating identity and address fields
        var calc = await _trustScoreCalculator.CalculateAsync(b.Id, cancellationToken);
        var score = await _context.TrustScores.FirstOrDefaultAsync(t => t.BusinessId == b.Id, cancellationToken);
        if (score == null)
        {
            score = new VeriPass.Domain.Entities.TrustScore { BusinessId = b.Id };
            _context.TrustScores.Add(score);
        }
        score.OverallScore = calc.OverallScore;
        score.IdentityScore = calc.IdentityScore;
        score.CredentialScore = calc.CredentialScore;
        score.ComplianceScore = calc.ComplianceScore;
        score.VerificationScore = calc.VerificationScore;
        score.RiskScore = calc.RiskScore;
        score.LastCalculatedAt = DateTime.UtcNow;

        // Synchronize BusinessPassport verification score
        var passport = await _context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == b.Id, cancellationToken);
        if (passport != null)
        {
            passport.VerificationScore = calc.OverallScore;
            passport.UpdatedAt = DateTime.UtcNow;
        }

        // Synchronize linked BuyerSupplierRelationships with authoritative risk level
        var relationships = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == b.Id)
            .ToListAsync(cancellationToken);
        var derivedRisk = SupplierRiskPolicy.DetermineRiskLevelSafe(calc.OverallScore);
        foreach (var rel in relationships)
        {
            rel.RiskLevel = derivedRisk;
            rel.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record AddBusinessAddressCommand(
    Guid BusinessId,
    string AddressType,
    string Line1,
    string? Line2,
    string City,
    string? District,
    string State,
    string Pincode,
    string Country,
    bool IsPrimary) : IRequest<Result<Guid>>;

public class AddBusinessAddressCommandHandler : IRequestHandler<AddBusinessAddressCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddBusinessAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddBusinessAddressCommand request, CancellationToken cancellationToken)
    {
        var address = new BusinessAddress
        {
            BusinessId = request.BusinessId,
            AddressType = request.AddressType,
            Line1 = request.Line1,
            Line2 = request.Line2,
            City = request.City,
            District = request.District,
            State = request.State,
            Pincode = request.Pincode,
            Country = request.Country,
            IsPrimary = request.IsPrimary,
            VerificationStatus = VerificationStatus.VERIFIED
        };

        _context.BusinessAddresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(address.Id);
    }
}

public record CreateBusinessCommand(
    Guid OrganizationId,
    string LegalName,
    string? TradeName,
    string? BusinessType,
    string? Pan,
    string? Gstin,
    string? Cin,
    string? UdyamNumber) : IRequest<Result<Guid>>;

public class CreateBusinessCommandHandler : IRequestHandler<CreateBusinessCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateBusinessCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateBusinessCommand request, CancellationToken cancellationToken)
    {
        var businessIdNumber = "VP-IND-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var business = new Business
        {
            OrganizationId = request.OrganizationId,
            BusinessIdNumber = businessIdNumber,
            LegalName = request.LegalName,
            TradeName = request.TradeName,
            BusinessType = request.BusinessType,
            Pan = request.Pan,
            Gstin = request.Gstin,
            Cin = request.Cin,
            UdyamNumber = request.UdyamNumber,
            VerificationStatus = VerificationStatus.PENDING
        };

        _context.Businesses.Add(business);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(business.Id);
    }
}

public record AddDirectorCommand(
    Guid BusinessId,
    string FullName,
    string Designation,
    string? Din,
    string? Pan) : IRequest<Result<Guid>>;

public class AddDirectorCommandHandler : IRequestHandler<AddDirectorCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddDirectorCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddDirectorCommand request, CancellationToken cancellationToken)
    {
        var director = new Director
        {
            BusinessId = request.BusinessId,
            FullName = request.FullName,
            Designation = request.Designation,
            Din = request.Din,
            PanMasked = !string.IsNullOrEmpty(request.Pan) && request.Pan.Length >= 4 ? $"••••••{request.Pan[^4..]}" : request.Pan,
            Status = "ACTIVE"
        };

        _context.Directors.Add(director);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(director.Id);
    }
}

public record GetDirectorsQuery(Guid BusinessId) : IRequest<Result<List<DirectorDto>>>;

public class GetDirectorsQueryHandler : IRequestHandler<GetDirectorsQuery, Result<List<DirectorDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDirectorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DirectorDto>>> Handle(GetDirectorsQuery request, CancellationToken cancellationToken)
    {
        var directors = await _context.Directors
            .AsNoTracking()
            .Where(d => d.BusinessId == request.BusinessId)
            .Select(d => new DirectorDto(
                d.Id,
                d.FullName,
                d.Designation,
                d.Din,
                d.PanMasked,
                d.AppointmentDate,
                d.Status))
            .ToListAsync(cancellationToken);

        return Result<List<DirectorDto>>.Success(directors);
    }
}

public record UpdateDirectorCommand(
    Guid DirectorId,
    string FullName,
    string Designation,
    string? Din) : IRequest<Result<bool>>;

public class UpdateDirectorCommandHandler : IRequestHandler<UpdateDirectorCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateDirectorCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateDirectorCommand request, CancellationToken cancellationToken)
    {
        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Id == request.DirectorId, cancellationToken);
        if (director == null) return Result<bool>.Failure("Director not found.");

        director.FullName = request.FullName;
        director.Designation = request.Designation;
        director.Din = request.Din;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record DeleteDirectorCommand(Guid DirectorId) : IRequest<Result<bool>>;

public class DeleteDirectorCommandHandler : IRequestHandler<DeleteDirectorCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteDirectorCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteDirectorCommand request, CancellationToken cancellationToken)
    {
        var director = await _context.Directors.FirstOrDefaultAsync(d => d.Id == request.DirectorId, cancellationToken);
        if (director == null) return Result<bool>.Failure("Director not found.");

        _context.Directors.Remove(director);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record AddBankAccountCommand(
    Guid BusinessId,
    string AccountHolderName,
    string BankName,
    string AccountNumber,
    string Ifsc,
    string AccountType) : IRequest<Result<Guid>>;

public class AddBankAccountCommandHandler : IRequestHandler<AddBankAccountCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public AddBankAccountCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(AddBankAccountCommand request, CancellationToken cancellationToken)
    {
        var last4 = request.AccountNumber.Length >= 4 ? request.AccountNumber[^4..] : request.AccountNumber;
        var account = new BankAccount
        {
            BusinessId = request.BusinessId,
            AccountHolderName = request.AccountHolderName,
            BankName = request.BankName,
            AccountNumberLast4 = last4,
            Ifsc = request.Ifsc,
            AccountType = request.AccountType,
            VerificationStatus = VerificationStatus.VERIFIED
        };

        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(account.Id);
    }
}

public record GetBankAccountsQuery(Guid BusinessId) : IRequest<Result<List<BankAccountDto>>>;

public class GetBankAccountsQueryHandler : IRequestHandler<GetBankAccountsQuery, Result<List<BankAccountDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBankAccountsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<BankAccountDto>>> Handle(GetBankAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _context.BankAccounts
            .AsNoTracking()
            .Where(b => b.BusinessId == request.BusinessId)
            .Select(b => new BankAccountDto(
                b.Id,
                b.AccountHolderName,
                b.BankName,
                b.AccountNumberLast4,
                b.Ifsc,
                b.AccountType,
                b.VerificationStatus.ToString(),
                b.VerifiedAt,
                b.IsPrimary))
            .ToListAsync(cancellationToken);

        return Result<List<BankAccountDto>>.Success(accounts);
    }
}

