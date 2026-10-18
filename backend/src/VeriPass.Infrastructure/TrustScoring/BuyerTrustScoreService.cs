using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Domain.TrustScoring;

namespace VeriPass.Infrastructure.TrustScoring;

public class BuyerTrustScoreService : IBuyerTrustScoreService
{
    private readonly IApplicationDbContext _context;
    private readonly IRuleBasedVerificationEngine _ruleEngine;
    private readonly ILogger<BuyerTrustScoreService> _logger;

    public BuyerTrustScoreService(
        IApplicationDbContext context,
        IRuleBasedVerificationEngine ruleEngine,
        ILogger<BuyerTrustScoreService> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }

    public BuyerTrustScoreService(
        IApplicationDbContext context,
        ILogger<BuyerTrustScoreService> logger)
        : this(context, new RuleBasedVerificationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<RuleBasedVerificationEngine>.Instance), logger)
    {
    }

    public record VerificationConfiguration(
        string ConfigurationSource, // "BUYER" or "PLATFORM_DEFAULT"
        int Version,
        IReadOnlyList<(string Type, bool IsRequired, int Weight, string? Rules)> Requirements);

    public async Task<BuyerTrustScoreDto> CalculateAndSaveScoreAsync(
        Guid buyerId,
        Guid supplierBusinessId,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve configuration hierarchy: Buyer profile takes priority; Platform Admin default rules are fallback
        var config = await ResolveActiveConfigurationAsync(buyerId, cancellationToken);

        // 2. Load supplier with credentials, documents, and verification cases
        var supplier = await _context.Businesses
            .Include(b => b.Credentials)
                .ThenInclude(c => c.CredentialType)
            .Include(b => b.Documents)
            .Include(b => b.BankAccounts)
            .Include(b => b.VerificationCases)
                .ThenInclude(vc => vc.Checks)
            .FirstOrDefaultAsync(b => b.Id == supplierBusinessId, cancellationToken);

        if (supplier == null)
        {
            throw new InvalidOperationException($"Supplier business {supplierBusinessId} was not found.");
        }

        var adminRules = await _context.ComplianceRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        // 3. Evaluate each credential requirement using RuleBasedVerificationEngine
        var calculationInputs = new List<CredentialInput>();
        var evalResults = new List<CredentialVerificationResult>();

        foreach (var req in config.Requirements)
        {
            var evalResult = _ruleEngine.EvaluateCredential(
                supplier,
                req.Type,
                req.IsRequired,
                req.Rules,
                adminRules);

            evalResults.Add(evalResult);

            calculationInputs.Add(new CredentialInput(
                Weight: req.Weight,
                IsRequired: req.IsRequired,
                Status: evalResult.FinalStatus,
                RuleFailed: evalResult.RuleFailed,
                CredentialType: req.Type,
                RuleFailedReason: evalResult.RuleFailedReason
            ));
        }

        // 4. Calculate score using pure domain engine
        var calcResult = VeriPass.Domain.TrustScoring.TrustScoreCalculator.Calculate(calculationInputs);

        var breakdownDtos = calcResult.Breakdown.Select((b, i) => new CredentialBreakdownDto(
            CredentialType: b.CredentialType,
            Weight: b.Weight,
            IsRequired: b.IsRequired,
            Status: b.Status,
            RuleFailed: b.RuleFailed,
            PointsEarned: b.PointsEarned,
            Contribution: b.Contribution,
            RuleFailedReason: b.RuleFailedReason,
            RuleDetails: evalResults.Count > i ? evalResults[i].RuleDetails : null
        )).ToList();

        // 5. Persist audit snapshot
        var snapshot = new TrustScoreSnapshot
        {
            BusinessId = supplierBusinessId,
            SupplierId = supplierBusinessId,
            BuyerId = buyerId,
            Score = calcResult.Score,
            Level = calcResult.TrustLevel.ToString(),
            Compliance = calcResult.ComplianceStatus.ToString(),
            ProfileVersion = config.Version,
            CalculatedAt = DateTime.UtcNow,
            BreakdownJson = JsonSerializer.Serialize(breakdownDtos),
            OverallScore = calcResult.Score,
            RiskScore = calcResult.RiskScore,
            CalculationVersion = $"v{config.Version}.0",
            Reason = config.ConfigurationSource == "BUYER" ? "BUYER_CALCULATION" : "PLATFORM_DEFAULT_CALCULATION",
            ConfigurationSource = config.ConfigurationSource,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrustScoreSnapshots.Add(snapshot);

        // 6. Synchronize relationship risk level if an active relationship exists
        var relationship = await _context.BuyerSupplierRelationships
            .FirstOrDefaultAsync(r => r.BuyerOrganizationId == buyerId && r.SupplierBusinessId == supplierBusinessId, cancellationToken);

        if (relationship != null)
        {
            relationship.RiskLevel = calcResult.TrustLevel switch
            {
                TrustLevel.High => RiskLevel.LOW,
                TrustLevel.Medium => RiskLevel.MEDIUM,
                _ => RiskLevel.HIGH
            };
            relationship.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new BuyerTrustScoreDto(
            BuyerId: buyerId,
            SupplierId: supplierBusinessId,
            SupplierName: supplier.LegalName,
            Score: calcResult.Score,
            TrustLevel: calcResult.TrustLevel.ToString(),
            RiskScore: calcResult.RiskScore,
            ComplianceStatus: calcResult.ComplianceStatus.ToString(),
            IsSelectable: calcResult.ComplianceStatus != ComplianceStatus.VerificationFailed,
            ProfileVersion: config.Version,
            CalculatedAt: snapshot.CalculatedAt,
            Breakdown: breakdownDtos,
            ConfigurationSource: config.ConfigurationSource
        );
    }

    public async Task<BuyerTrustScoreDto> GetLatestScoreAsync(
        Guid buyerId,
        Guid supplierBusinessId,
        CancellationToken cancellationToken = default)
    {
        var config = await ResolveActiveConfigurationAsync(buyerId, cancellationToken);

        var latestSnapshot = await _context.TrustScoreSnapshots
            .Where(s => s.BuyerId == buyerId && s.SupplierId == supplierBusinessId)
            .OrderByDescending(s => s.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSnapshot != null &&
            latestSnapshot.ProfileVersion == config.Version &&
            (latestSnapshot.ConfigurationSource ?? "BUYER") == config.ConfigurationSource &&
            !string.IsNullOrWhiteSpace(latestSnapshot.BreakdownJson))
        {
            try
            {
                var breakdown = JsonSerializer.Deserialize<List<CredentialBreakdownDto>>(latestSnapshot.BreakdownJson);
                if (breakdown != null)
                {
                    var supplier = await _context.Businesses
                        .Select(b => new { b.Id, b.LegalName })
                        .FirstOrDefaultAsync(b => b.Id == supplierBusinessId, cancellationToken);

                    var compliance = Enum.TryParse<ComplianceStatus>(latestSnapshot.Compliance, out var cs)
                        ? cs
                        : ComplianceStatus.Incomplete;

                    return new BuyerTrustScoreDto(
                        BuyerId: buyerId,
                        SupplierId: supplierBusinessId,
                        SupplierName: supplier?.LegalName ?? "Supplier Business",
                        Score: latestSnapshot.Score,
                        TrustLevel: latestSnapshot.Level,
                        RiskScore: 100 - latestSnapshot.Score,
                        ComplianceStatus: latestSnapshot.Compliance,
                        IsSelectable: compliance != ComplianceStatus.VerificationFailed,
                        ProfileVersion: latestSnapshot.ProfileVersion,
                        CalculatedAt: latestSnapshot.CalculatedAt,
                        Breakdown: breakdown,
                        ConfigurationSource: latestSnapshot.ConfigurationSource ?? config.ConfigurationSource
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize snapshot breakdown for buyer {BuyerId} and supplier {SupplierId}", buyerId, supplierBusinessId);
            }
        }

        return await CalculateAndSaveScoreAsync(buyerId, supplierBusinessId, cancellationToken);
    }

    public async Task<List<BuyerTrustScoreDto>> GetBatchScoresAsync(
        Guid buyerId,
        IEnumerable<Guid> supplierBusinessIds,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BuyerTrustScoreDto>();
        foreach (var supplierId in supplierBusinessIds.Distinct())
        {
            try
            {
                var score = await GetLatestScoreAsync(buyerId, supplierId, cancellationToken);
                results.Add(score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trust score for supplier {SupplierId} in batch for buyer {BuyerId}", supplierId, buyerId);
            }
        }
        return results;
    }

    public async Task RecalculateAllForSupplierAsync(
        Guid supplierBusinessId,
        CancellationToken cancellationToken = default)
    {
        var relBuyerIds = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == supplierBusinessId)
            .Select(r => r.BuyerOrganizationId)
            .ToListAsync(cancellationToken);

        var onbBuyerIds = await _context.OnboardingRequests
            .Where(o => o.SupplierBusinessId == supplierBusinessId)
            .Select(o => o.BuyerOrganizationId)
            .ToListAsync(cancellationToken);

        var shortBuyerIds = await _context.SupplierShortlistItems
            .Where(s => s.BusinessId == supplierBusinessId)
            .Select(s => s.Shortlist.BuyerOrganizationId)
            .ToListAsync(cancellationToken);

        var pastBuyerIds = await _context.TrustScoreSnapshots
            .Where(s => s.SupplierId == supplierBusinessId && s.BuyerId != null)
            .Select(s => s.BuyerId!.Value)
            .ToListAsync(cancellationToken);

        var allBuyerIds = relBuyerIds
            .Concat(onbBuyerIds)
            .Concat(shortBuyerIds)
            .Concat(pastBuyerIds)
            .Distinct()
            .ToList();

        _logger.LogInformation("Recalculating buyer trust scores for supplier {SupplierId} across {Count} buyers.", supplierBusinessId, allBuyerIds.Count);

        foreach (var buyerId in allBuyerIds)
        {
            try
            {
                await CalculateAndSaveScoreAsync(buyerId, supplierBusinessId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate trust score for buyer {BuyerId} and supplier {SupplierId}", buyerId, supplierBusinessId);
            }
        }
    }

    public async Task RecalculateAllForBuyerAsync(
        Guid buyerId,
        CancellationToken cancellationToken = default)
    {
        var relSuppliers = await _context.BuyerSupplierRelationships
            .Where(r => r.BuyerOrganizationId == buyerId)
            .Select(r => r.SupplierBusinessId)
            .ToListAsync(cancellationToken);

        var onbSuppliers = await _context.OnboardingRequests
            .Where(o => o.BuyerOrganizationId == buyerId)
            .Select(o => o.SupplierBusinessId)
            .ToListAsync(cancellationToken);

        var shortSuppliers = await _context.SupplierShortlistItems
            .Where(s => s.Shortlist.BuyerOrganizationId == buyerId)
            .Select(s => s.BusinessId)
            .ToListAsync(cancellationToken);

        var pastSuppliers = await _context.TrustScoreSnapshots
            .Where(s => s.BuyerId == buyerId && s.SupplierId != null)
            .Select(s => s.SupplierId!.Value)
            .ToListAsync(cancellationToken);

        var allSupplierIds = relSuppliers
            .Concat(onbSuppliers)
            .Concat(shortSuppliers)
            .Concat(pastSuppliers)
            .Distinct()
            .ToList();

        _logger.LogInformation("Recalculating buyer trust scores for buyer {BuyerId} across {Count} suppliers.", buyerId, allSupplierIds.Count);

        foreach (var supplierId in allSupplierIds)
        {
            try
            {
                await CalculateAndSaveScoreAsync(buyerId, supplierId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recalculate trust score for buyer {BuyerId} and supplier {SupplierId}", buyerId, supplierId);
            }
        }
    }

    private async Task<VerificationConfiguration> ResolveActiveConfigurationAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        var profile = await _context.BuyerVerificationProfiles
            .Include(p => p.Requirements)
            .Where(p => p.BuyerOrganizationId == buyerId && p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile != null && profile.Requirements.Any())
        {
            var reqs = profile.Requirements
                .Select(r => (r.Type, r.IsRequired, r.Weight, (string?)r.Rules))
                .ToList();

            return new VerificationConfiguration("BUYER", profile.Version, reqs);
        }

        // Platform Admin Default Rules Fallback
        var adminRules = await _context.ComplianceRules
            .AsNoTracking()
            .Where(r => r.IsActive && r.DefaultWeight > 0)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

        if (adminRules.Any())
        {
            var reqs = adminRules.Select(r => (
                Type: r.CredentialType ?? r.Code,
                IsRequired: r.IsRequired,
                Weight: r.DefaultWeight,
                Rules: (string?)r.ValidationType
            )).ToList();

            return new VerificationConfiguration("PLATFORM_DEFAULT", 1, reqs);
        }

        // Platform Admin Default Baseline if DB has no rules configured yet
        var defaultBaseline = new List<(string Type, bool IsRequired, int Weight, string? Rules)>
        {
            ("GSTIN", true, 30, "CHECKSUM"),
            ("PAN", true, 15, "STRUCTURE"),
            ("UDYAM", true, 15, "FORMAT"),
            ("BANK", true, 25, "DATA_MATCH"),
            ("BUSINESS_REGISTRATION", false, 15, "FORMAT")
        };

        return new VerificationConfiguration("PLATFORM_DEFAULT", 1, defaultBaseline);
    }

    private static (string status, bool ruleFailed, string? ruleFailedReason) EvaluateSupplierCredentialStatus(
        Business supplier,
        CredentialRequirement requirement)
    {
        var reqType = requirement.Type.Trim().ToUpperInvariant();
        var normReqType = DocumentScoringPolicy.NormalizeType(reqType);

        // Find credentials matching type
        var matchingCredentials = supplier.Credentials
            .Where(c => c.CredentialType != null &&
                        (c.CredentialType.Code.Equals(reqType, StringComparison.OrdinalIgnoreCase) ||
                         DocumentScoringPolicy.NormalizeType(c.CredentialType.Code) == normReqType))
            .ToList();

        // Find documents matching type
        var matchingDocuments = supplier.Documents
            .Where(d => DocumentScoringPolicy.NormalizeType(d.DocumentType) == normReqType ||
                        d.DocumentType.Equals(reqType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Find verification cases matching type
        var matchingCases = supplier.VerificationCases
            .Where(vc => DocumentScoringPolicy.NormalizeType(vc.VerificationType) == normReqType ||
                         vc.VerificationType.Equals(reqType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Bank Account check
        bool isBankType = normReqType == "BANK" || reqType.Contains("BANK");
        bool hasVerifiedBankAccount = isBankType && supplier.BankAccounts.Any(b => b.VerificationStatus == VerificationStatus.VERIFIED);
        bool hasRejectedBankAccount = isBankType && supplier.BankAccounts.Any(b => b.VerificationStatus == VerificationStatus.REJECTED);
        bool hasPendingBankAccount = isBankType && supplier.BankAccounts.Any(b => b.VerificationStatus == VerificationStatus.UNVERIFIED);

        // GST check
        bool isGstType = normReqType == "GST";
        bool hasDirectGstVerified = isGstType && !string.IsNullOrWhiteSpace(supplier.Gstin) && supplier.VerificationStatus == VerificationStatus.VERIFIED;

        // PAN check
        bool isPanType = normReqType == "PAN";
        bool hasDirectPanVerified = isPanType && !string.IsNullOrWhiteSpace(supplier.Pan) && supplier.VerificationStatus == VerificationStatus.VERIFIED;

        // 1. Check EXPIRED
        bool isExpired = matchingCredentials.Any(c => (c.ExpiryDate.HasValue && c.ExpiryDate.Value < DateTime.UtcNow) ||
                                                      c.Status == CredentialStatus.EXPIRED ||
                                                      c.VerificationStatus == VerificationStatus.EXPIRED);

        if (isExpired)
        {
            return ("EXPIRED", false, null);
        }

        // 2. Check REJECTED
        bool isRejected = matchingCredentials.Any(c => c.Status == CredentialStatus.REJECTED || c.VerificationStatus == VerificationStatus.REJECTED) ||
                          matchingDocuments.Any(d => d.Status == DocumentStatus.REJECTED) ||
                          matchingCases.Any(c => c.FinalDecision == VerificationDecision.REJECTED) ||
                          hasRejectedBankAccount;

        if (isRejected)
        {
            return ("REJECTED", false, null);
        }

        // 3. Check APPROVED / VERIFIED
        bool isApproved = matchingCredentials.Any(c => c.Status == CredentialStatus.VERIFIED || c.VerificationStatus == VerificationStatus.VERIFIED) ||
                          matchingDocuments.Any(d => d.Status == DocumentStatus.APPROVED) ||
                          matchingCases.Any(c => c.FinalDecision == VerificationDecision.APPROVED || c.Status == VerificationStatus.VERIFIED) ||
                          hasVerifiedBankAccount ||
                          hasDirectGstVerified ||
                          hasDirectPanVerified;

        // 4. Check rule evaluation if approved
        bool ruleFailed = false;
        string? ruleFailedReason = null;

        if (isApproved && !string.IsNullOrWhiteSpace(requirement.Rules))
        {
            var rulesUpper = requirement.Rules.ToUpperInvariant();

            // Name match rule check
            if (rulesUpper.Contains("NAME_MATCH"))
            {
                var failedNameCheck = matchingCases
                    .SelectMany(c => c.Checks)
                    .FirstOrDefault(chk => chk.CheckType.Contains("NAME", StringComparison.OrdinalIgnoreCase) &&
                                           (chk.Status == VerificationStatus.REJECTED || chk.Status == VerificationStatus.FLAGGED));

                if (failedNameCheck != null)
                {
                    ruleFailed = true;
                    ruleFailedReason = failedNameCheck.ErrorMessage ?? "Entity legal name mismatch against sovereign registry";
                }
            }

            // Active status rule check
            if (!ruleFailed && (rulesUpper.Contains("ACTIVE_STATUS") || rulesUpper.Contains("ACTIVE_REGISTRY")))
            {
                var failedStatusCheck = matchingCases
                    .SelectMany(c => c.Checks)
                    .FirstOrDefault(chk => (chk.CheckType.Contains("STATUS", StringComparison.OrdinalIgnoreCase) ||
                                            chk.CheckType.Contains("ACTIVE", StringComparison.OrdinalIgnoreCase)) &&
                                           (chk.Status == VerificationStatus.REJECTED || chk.Status == VerificationStatus.FLAGGED));

                if (failedStatusCheck != null)
                {
                    ruleFailed = true;
                    ruleFailedReason = failedStatusCheck.ErrorMessage ?? "Credential registry status is inactive or flagged";
                }
            }
        }

        if (isApproved)
        {
            return ("APPROVED", ruleFailed, ruleFailedReason);
        }

        // 5. Check PENDING
        bool isPending = matchingCredentials.Any(c => c.Status == CredentialStatus.SUBMITTED || c.VerificationStatus == VerificationStatus.PENDING || c.VerificationStatus == VerificationStatus.IN_PROGRESS) ||
                         matchingDocuments.Any(d => d.Status == DocumentStatus.UPLOADED || d.Status == DocumentStatus.PROCESSING) ||
                         matchingCases.Any(c => c.Status == VerificationStatus.IN_PROGRESS || c.Status == VerificationStatus.PENDING) ||
                         hasPendingBankAccount;

        if (isPending)
        {
            return ("PENDING", false, null);
        }

        // 6. Otherwise MISSING
        return ("MISSING", false, null);
    }
}
