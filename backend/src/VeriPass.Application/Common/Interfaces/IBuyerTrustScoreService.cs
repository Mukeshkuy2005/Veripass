namespace VeriPass.Application.Common.Interfaces;

public record CredentialBreakdownDto(
    string CredentialType,
    int Weight,
    bool IsRequired,
    string Status,
    bool RuleFailed,
    int PointsEarned,
    int Contribution,
    string? RuleFailedReason,
    IReadOnlyList<RuleExecutionDetail>? RuleDetails = null);

public record BuyerTrustScoreDto(
    Guid BuyerId,
    Guid SupplierId,
    string SupplierName,
    int Score,
    string TrustLevel,
    int RiskScore,
    string ComplianceStatus,
    bool IsSelectable,
    int ProfileVersion,
    DateTime CalculatedAt,
    List<CredentialBreakdownDto> Breakdown,
    string ConfigurationSource = "BUYER");

public interface IBuyerTrustScoreService
{
    Task<BuyerTrustScoreDto> CalculateAndSaveScoreAsync(Guid buyerId, Guid supplierBusinessId, CancellationToken cancellationToken = default);
    Task<BuyerTrustScoreDto> GetLatestScoreAsync(Guid buyerId, Guid supplierBusinessId, CancellationToken cancellationToken = default);
    Task<List<BuyerTrustScoreDto>> GetBatchScoresAsync(Guid buyerId, IEnumerable<Guid> supplierBusinessIds, CancellationToken cancellationToken = default);
    Task RecalculateAllForSupplierAsync(Guid supplierBusinessId, CancellationToken cancellationToken = default);
    Task RecalculateAllForBuyerAsync(Guid buyerId, CancellationToken cancellationToken = default);
}
