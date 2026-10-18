using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.TrustScore;

public record RequirementConfigDto(
    string Type,
    bool IsRequired,
    int Weight,
    string? Rules,
    Guid? CredentialTypeId = null);

public record BuyerVerificationProfileDto(
    Guid Id,
    Guid BuyerOrganizationId,
    string Name,
    string? Description,
    int Version,
    bool IsActive,
    bool IsDefault,
    int TotalWeight,
    List<RequirementConfigDto> Requirements);

public record TrustScoreSnapshotDto(
    Guid Id,
    int Score,
    string Level,
    string Compliance,
    int ProfileVersion,
    DateTime CalculatedAt,
    string? Reason,
    List<CredentialBreakdownDto>? Breakdown,
    string ConfigurationSource = "BUYER");

// 1. Get Profile Query
public record GetBuyerVerificationProfileQuery(Guid BuyerOrganizationId) : IRequest<Result<BuyerVerificationProfileDto>>;

public class GetBuyerVerificationProfileQueryHandler : IRequestHandler<GetBuyerVerificationProfileQuery, Result<BuyerVerificationProfileDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerVerificationProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BuyerVerificationProfileDto>> Handle(GetBuyerVerificationProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _context.BuyerVerificationProfiles
            .Include(p => p.Requirements)
            .Where(p => p.BuyerOrganizationId == request.BuyerOrganizationId && p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile == null)
        {
            // Fall back to Platform Admin Default Rules
            var adminRules = await _context.ComplianceRules
                .AsNoTracking()
                .Where(r => r.IsActive && r.DefaultWeight > 0)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            var defaultReqs = adminRules.Any()
                ? adminRules.Select(r => new RequirementConfigDto(
                    Type: r.CredentialType ?? r.Code,
                    IsRequired: r.IsRequired,
                    Weight: r.DefaultWeight,
                    Rules: r.ValidationType,
                    CredentialTypeId: null)).ToList()
                : new List<RequirementConfigDto>
                {
                    new("GSTIN", true, 30, "CHECKSUM"),
                    new("PAN", true, 15, "STRUCTURE"),
                    new("UDYAM", true, 15, "FORMAT"),
                    new("BANK", true, 25, "DATA_MATCH"),
                    new("BUSINESS_REGISTRATION", false, 15, "FORMAT")
                };

            var totalWeight = defaultReqs.Sum(r => r.Weight);

            return Result<BuyerVerificationProfileDto>.Success(new BuyerVerificationProfileDto(
                Id: Guid.Empty,
                BuyerOrganizationId: request.BuyerOrganizationId,
                Name: "Platform Admin Default Verification Profile",
                Description: "Platform Admin baseline verification rules and credential weights fallback.",
                Version: 1,
                IsActive: true,
                IsDefault: true,
                TotalWeight: totalWeight,
                Requirements: defaultReqs
            ));
        }

        return Result<BuyerVerificationProfileDto>.Success(new BuyerVerificationProfileDto(
            Id: profile.Id,
            BuyerOrganizationId: profile.BuyerOrganizationId,
            Name: profile.Name,
            Description: profile.Description,
            Version: profile.Version,
            IsActive: profile.IsActive,
            IsDefault: profile.IsDefault,
            TotalWeight: profile.TotalWeight,
            Requirements: profile.Requirements.Select(r => new RequirementConfigDto(
                Type: r.Type,
                IsRequired: r.IsRequired,
                Weight: r.Weight,
                Rules: r.Rules,
                CredentialTypeId: r.CredentialTypeId
            )).ToList()
        ));
    }
}

// 2. Update Profile Command
public record UpdateBuyerVerificationProfileCommand(
    Guid BuyerOrganizationId,
    string Name,
    string? Description,
    List<RequirementConfigDto> Requirements) : IRequest<Result<BuyerVerificationProfileDto>>;

public class UpdateBuyerVerificationProfileCommandHandler : IRequestHandler<UpdateBuyerVerificationProfileCommand, Result<BuyerVerificationProfileDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IBuyerTrustScoreService _buyerTrustScoreService;

    public UpdateBuyerVerificationProfileCommandHandler(
        IApplicationDbContext context,
        IBuyerTrustScoreService buyerTrustScoreService)
    {
        _context = context;
        _buyerTrustScoreService = buyerTrustScoreService;
    }

    public async Task<Result<BuyerVerificationProfileDto>> Handle(UpdateBuyerVerificationProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.Requirements == null || request.Requirements.Count == 0)
        {
            return Result<BuyerVerificationProfileDto>.Failure("At least one credential requirement must be defined.");
        }

        // Locked Decision: Weights must sum to exactly 100; reject on save otherwise.
        var totalWeight = request.Requirements.Sum(r => r.Weight);
        if (totalWeight != 100)
        {
            return Result<BuyerVerificationProfileDto>.Failure($"Buyer profile weights must sum to exactly 100. Current sum is {totalWeight}.");
        }

        foreach (var req in request.Requirements)
        {
            if (string.IsNullOrWhiteSpace(req.Type))
            {
                return Result<BuyerVerificationProfileDto>.Failure("Credential requirement type is required.");
            }
            if (req.Weight <= 0 || req.Weight > 100)
            {
                return Result<BuyerVerificationProfileDto>.Failure($"Weight for requirement '{req.Type}' must be between 1 and 100.");
            }
        }

        var profile = await _context.BuyerVerificationProfiles
            .Include(p => p.Requirements)
            .Where(p => p.BuyerOrganizationId == request.BuyerOrganizationId && p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile == null)
        {
            profile = new BuyerVerificationProfile
            {
                BuyerOrganizationId = request.BuyerOrganizationId,
                Name = request.Name,
                Description = request.Description,
                Version = 1,
                IsActive = true,
                IsDefault = true,
                TotalWeight = 100
            };
            _context.BuyerVerificationProfiles.Add(profile);
        }
        else
        {
            profile.Name = request.Name;
            profile.Description = request.Description;
            profile.Version += 1;
            profile.TotalWeight = 100;
            profile.UpdatedAt = DateTime.UtcNow;

            var existingList = profile.Requirements.ToList();
            var newReqList = request.Requirements.ToList();

            int i = 0;
            for (; i < Math.Min(existingList.Count, newReqList.Count); i++)
            {
                var existing = existingList[i];
                var req = newReqList[i];
                existing.Type = req.Type.Trim().ToUpperInvariant();
                existing.IsRequired = req.IsRequired;
                existing.Weight = req.Weight;
                existing.Rules = req.Rules;
                existing.CredentialTypeId = req.CredentialTypeId;
            }

            for (; i < newReqList.Count; i++)
            {
                var req = newReqList[i];
                profile.Requirements.Add(new CredentialRequirement
                {
                    ProfileId = profile.Id,
                    Type = req.Type.Trim().ToUpperInvariant(),
                    IsRequired = req.IsRequired,
                    Weight = req.Weight,
                    Rules = req.Rules,
                    CredentialTypeId = req.CredentialTypeId
                });
            }

            for (; i < existingList.Count; i++)
            {
                var toRemove = existingList[i];
                profile.Requirements.Remove(toRemove);
                _context.CredentialRequirements.Remove(toRemove);
            }
        }

        profile.ValidateWeights();

        await _context.SaveChangesAsync(cancellationToken);

        // Recalculate trust scores for all affected suppliers associated with this buyer
        await _buyerTrustScoreService.RecalculateAllForBuyerAsync(request.BuyerOrganizationId, cancellationToken);

        return Result<BuyerVerificationProfileDto>.Success(new BuyerVerificationProfileDto(
            Id: profile.Id,
            BuyerOrganizationId: profile.BuyerOrganizationId,
            Name: profile.Name,
            Description: profile.Description,
            Version: profile.Version,
            IsActive: profile.IsActive,
            IsDefault: profile.IsDefault,
            TotalWeight: profile.TotalWeight,
            Requirements: profile.Requirements.Select(r => new RequirementConfigDto(
                Type: r.Type,
                IsRequired: r.IsRequired,
                Weight: r.Weight,
                Rules: r.Rules,
                CredentialTypeId: r.CredentialTypeId
            )).ToList()
        ));
    }
}

// 3. Get Buyer Trust Score Query
public record GetBuyerTrustScoreQuery(Guid BuyerOrganizationId, Guid SupplierBusinessId) : IRequest<Result<BuyerTrustScoreDto>>;

public class GetBuyerTrustScoreQueryHandler : IRequestHandler<GetBuyerTrustScoreQuery, Result<BuyerTrustScoreDto>>
{
    private readonly IBuyerTrustScoreService _buyerTrustScoreService;

    public GetBuyerTrustScoreQueryHandler(IBuyerTrustScoreService buyerTrustScoreService)
    {
        _buyerTrustScoreService = buyerTrustScoreService;
    }

    public async Task<Result<BuyerTrustScoreDto>> Handle(GetBuyerTrustScoreQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var score = await _buyerTrustScoreService.GetLatestScoreAsync(request.BuyerOrganizationId, request.SupplierBusinessId, cancellationToken);
            return Result<BuyerTrustScoreDto>.Success(score);
        }
        catch (Exception ex)
        {
            return Result<BuyerTrustScoreDto>.Failure(ex.Message);
        }
    }
}

// 4. Batch Trust Scores Query
public record GetBatchBuyerTrustScoresQuery(Guid BuyerOrganizationId, List<Guid> SupplierBusinessIds) : IRequest<Result<List<BuyerTrustScoreDto>>>;

public class GetBatchBuyerTrustScoresQueryHandler : IRequestHandler<GetBatchBuyerTrustScoresQuery, Result<List<BuyerTrustScoreDto>>>
{
    private readonly IBuyerTrustScoreService _buyerTrustScoreService;

    public GetBatchBuyerTrustScoresQueryHandler(IBuyerTrustScoreService buyerTrustScoreService)
    {
        _buyerTrustScoreService = buyerTrustScoreService;
    }

    public async Task<Result<List<BuyerTrustScoreDto>>> Handle(GetBatchBuyerTrustScoresQuery request, CancellationToken cancellationToken)
    {
        var scores = await _buyerTrustScoreService.GetBatchScoresAsync(request.BuyerOrganizationId, request.SupplierBusinessIds, cancellationToken);
        return Result<List<BuyerTrustScoreDto>>.Success(scores);
    }
}

// 5. Get History Snapshots Query
public record GetBuyerTrustScoreHistoryQuery(Guid BuyerOrganizationId, Guid SupplierBusinessId) : IRequest<Result<List<TrustScoreSnapshotDto>>>;

public class GetBuyerTrustScoreHistoryQueryHandler : IRequestHandler<GetBuyerTrustScoreHistoryQuery, Result<List<TrustScoreSnapshotDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBuyerTrustScoreHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TrustScoreSnapshotDto>>> Handle(GetBuyerTrustScoreHistoryQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _context.TrustScoreSnapshots
            .Where(s => s.BuyerId == request.BuyerOrganizationId && s.SupplierId == request.SupplierBusinessId)
            .OrderByDescending(s => s.CalculatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var dtos = snapshots.Select(s =>
        {
            List<CredentialBreakdownDto>? breakdown = null;
            if (!string.IsNullOrWhiteSpace(s.BreakdownJson))
            {
                try
                {
                    breakdown = JsonSerializer.Deserialize<List<CredentialBreakdownDto>>(s.BreakdownJson);
                }
                catch
                {
                    // Ignore parse errors on legacy or corrupted snapshots
                }
            }

            return new TrustScoreSnapshotDto(
                Id: s.Id,
                Score: s.Score,
                Level: s.Level,
                Compliance: s.Compliance,
                ProfileVersion: s.ProfileVersion,
                CalculatedAt: s.CalculatedAt,
                Reason: s.Reason,
                Breakdown: breakdown,
                ConfigurationSource: s.ConfigurationSource ?? "BUYER"
            );
        }).ToList();

        return Result<List<TrustScoreSnapshotDto>>.Success(dtos);
    }
}
