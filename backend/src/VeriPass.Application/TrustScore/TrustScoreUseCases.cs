using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.TrustScore;

public record RecalculateTrustScoreCommand(Guid BusinessId, string Reason = "MANUAL_RECALCULATION") : IRequest<Result<TrustScoreCalculationResult>>;

public class RecalculateTrustScoreCommandHandler : IRequestHandler<RecalculateTrustScoreCommand, Result<TrustScoreCalculationResult>>
{
    private readonly ITrustScoreCalculator _calculator;
    private readonly IApplicationDbContext _context;

    public RecalculateTrustScoreCommandHandler(ITrustScoreCalculator calculator, IApplicationDbContext context)
    {
        _calculator = calculator;
        _context = context;
    }

    public async Task<Result<TrustScoreCalculationResult>> Handle(RecalculateTrustScoreCommand request, CancellationToken cancellationToken)
    {
        var result = await _calculator.CalculateAsync(request.BusinessId, cancellationToken);

        var existingTrustScore = await _context.TrustScores
            .FirstOrDefaultAsync(t => t.BusinessId == request.BusinessId, cancellationToken);

        if (existingTrustScore != null)
        {
            existingTrustScore.OverallScore = result.OverallScore;
            existingTrustScore.IdentityScore = result.IdentityScore;
            existingTrustScore.CredentialScore = result.CredentialScore;
            existingTrustScore.ComplianceScore = result.ComplianceScore;
            existingTrustScore.VerificationScore = result.VerificationScore;
            existingTrustScore.RiskScore = result.RiskScore;
            existingTrustScore.LastCalculatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.TrustScores.Add(new VeriPass.Domain.Entities.TrustScore
            {
                BusinessId = request.BusinessId,
                OverallScore = result.OverallScore,
                IdentityScore = result.IdentityScore,
                CredentialScore = result.CredentialScore,
                ComplianceScore = result.ComplianceScore,
                VerificationScore = result.VerificationScore,
                RiskScore = result.RiskScore,
                CalculationVersion = result.Version,
                LastCalculatedAt = DateTime.UtcNow
            });
        }

        // Keep any linked buyer-supplier relationships in atomic sync with the authoritative risk level
        var relationships = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == request.BusinessId)
            .ToListAsync(cancellationToken);
        var derivedRisk = SupplierRiskPolicy.DetermineRiskLevelSafe(result.OverallScore);
        foreach (var rel in relationships)
        {
            rel.RiskLevel = derivedRisk;
        }

        var snapshot = new TrustScoreSnapshot
        {
            BusinessId = request.BusinessId,
            OverallScore = result.OverallScore,
            IdentityScore = result.IdentityScore,
            CredentialScore = result.CredentialScore,
            ComplianceScore = result.ComplianceScore,
            VerificationScore = result.VerificationScore,
            RiskScore = result.RiskScore,
            CalculationVersion = result.Version,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrustScoreSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TrustScoreCalculationResult>.Success(result);
    }
}

public record GetTrustScoreQuery(Guid BusinessId) : IRequest<Result<TrustScoreDto>>;

public record TrustScoreDto(
    int OverallScore,
    int IdentityScore,
    int CredentialScore,
    int ComplianceScore,
    int VerificationScore,
    int RiskScore,
    string Version,
    DateTime LastCalculatedAt);

public class GetTrustScoreQueryHandler : IRequestHandler<GetTrustScoreQuery, Result<TrustScoreDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTrustScoreQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TrustScoreDto>> Handle(GetTrustScoreQuery request, CancellationToken cancellationToken)
    {
        var ts = await _context.TrustScores
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == request.BusinessId, cancellationToken);

        if (ts == null)
            return Result<TrustScoreDto>.Failure("Trust score not found.");

        return Result<TrustScoreDto>.Success(new TrustScoreDto(
            ts.OverallScore,
            ts.IdentityScore,
            ts.CredentialScore,
            ts.ComplianceScore,
            ts.VerificationScore,
            ts.RiskScore,
            ts.CalculationVersion,
            ts.LastCalculatedAt));
    }
}
