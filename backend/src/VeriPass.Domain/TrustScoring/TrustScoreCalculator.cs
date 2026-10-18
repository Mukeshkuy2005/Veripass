namespace VeriPass.Domain.TrustScoring;

public enum TrustLevel
{
    Low,     // 0 - 34
    Medium,  // 35 - 70
    High     // 71 - 100
}

public enum ComplianceStatus
{
    Compliant,
    Incomplete,
    VerificationFailed
}

public record CredentialInput(
    int Weight,
    bool IsRequired,
    string Status,
    bool RuleFailed = false,
    string? CredentialType = null,
    string? RuleFailedReason = null);

public record CredentialBreakdown(
    string CredentialType,
    int Weight,
    bool IsRequired,
    string Status,
    bool RuleFailed,
    int PointsEarned,
    int Contribution,
    string? RuleFailedReason);

public record TrustScoreCalculationResult(
    int Score,
    TrustLevel TrustLevel,
    int RiskScore,
    ComplianceStatus ComplianceStatus,
    IReadOnlyList<CredentialBreakdown> Breakdown);

/// <summary>
/// Pure static calculation engine for buyer-specific weighted Trust Scores.
/// Has zero EF/DB or I/O dependencies.
/// </summary>
public static class TrustScoreCalculator
{
    public static TrustScoreCalculationResult Calculate(IEnumerable<CredentialInput>? credentials)
    {
        var list = credentials?.ToList() ?? new List<CredentialInput>();
        int totalWeight = list.Sum(c => c.Weight);

        // Locked Decision: Handle sum(W)=0 by returning score 0 and Incomplete
        if (totalWeight <= 0)
        {
            var emptyBreakdown = list.Select((c, idx) => new CredentialBreakdown(
                CredentialType: c.CredentialType ?? $"CRED_{idx + 1}",
                Weight: c.Weight,
                IsRequired: c.IsRequired,
                Status: NormalizeStatus(c.Status),
                RuleFailed: c.RuleFailed,
                PointsEarned: 0,
                Contribution: 0,
                RuleFailedReason: c.RuleFailedReason
            )).ToList();

            return new TrustScoreCalculationResult(
                Score: 0,
                TrustLevel: TrustLevel.Low,
                RiskScore: 100,
                ComplianceStatus: ComplianceStatus.Incomplete,
                Breakdown: emptyBreakdown
            );
        }

        long weightedContributionSum = 0;
        bool hasRequiredFailure = false;
        bool hasRequiredIncomplete = false;

        var breakdowns = new List<CredentialBreakdown>(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var status = NormalizeStatus(item.Status);

            // Locked Decision: S_i: APPROVED=1; REJECTED, PENDING, EXPIRED, MISSING = 0.
            // Failed verification rule forces S_i = 0.
            int contribution = (status == "APPROVED" && !item.RuleFailed) ? 1 : 0;
            int pointsEarned = item.Weight * contribution;
            weightedContributionSum += pointsEarned;

            // Locked Decision: Separate ComplianceStatus from the score:
            // any required credential REJECTED/EXPIRED or failed rule -> VerificationFailed
            // any required PENDING/MISSING -> Incomplete; otherwise Compliant.
            if (item.IsRequired)
            {
                if (status == "REJECTED" || status == "EXPIRED" || item.RuleFailed)
                {
                    hasRequiredFailure = true;
                }
                else if (status == "PENDING" || status == "MISSING")
                {
                    hasRequiredIncomplete = true;
                }
            }

            breakdowns.Add(new CredentialBreakdown(
                CredentialType: item.CredentialType ?? $"CRED_{i + 1}",
                Weight: item.Weight,
                IsRequired: item.IsRequired,
                Status: status,
                RuleFailed: item.RuleFailed,
                PointsEarned: pointsEarned,
                Contribution: contribution,
                RuleFailedReason: item.RuleFailedReason
            ));
        }

        // Locked Decision: Score = sum(W_i * S_i) / sum(W_i) * 100, rounded to nearest integer (half away from zero)
        double rawScore = ((double)weightedContributionSum / totalWeight) * 100.0;
        int score = (int)Math.Round(rawScore, MidpointRounding.AwayFromZero);
        score = Math.Clamp(score, 0, 100);

        // Locked Decision: Banding: 0-34 Low, 35-70 Medium, 71-100 High
        var trustLevel = score switch
        {
            <= 34 => TrustLevel.Low,
            <= 70 => TrustLevel.Medium,
            _ => TrustLevel.High
        };

        // Locked Decision: Risk = 100 - Score, derived at read time
        int riskScore = 100 - score;

        // Determine ComplianceStatus
        ComplianceStatus complianceStatus;
        if (hasRequiredFailure)
        {
            complianceStatus = ComplianceStatus.VerificationFailed;
        }
        else if (hasRequiredIncomplete)
        {
            complianceStatus = ComplianceStatus.Incomplete;
        }
        else
        {
            complianceStatus = ComplianceStatus.Compliant;
        }

        return new TrustScoreCalculationResult(
            Score: score,
            TrustLevel: trustLevel,
            RiskScore: riskScore,
            ComplianceStatus: complianceStatus,
            Breakdown: breakdowns
        );
    }

    /// <summary>
    /// Overload for tuple input matching the signature: (weight, isRequired, status, ruleFailed).
    /// </summary>
    public static TrustScoreCalculationResult Calculate(
        IEnumerable<(int weight, bool isRequired, string status, bool ruleFailed)> credentials)
    {
        var inputs = credentials?.Select(c => new CredentialInput(
            Weight: c.weight,
            IsRequired: c.isRequired,
            Status: c.status,
            RuleFailed: c.ruleFailed
        ));

        return Calculate(inputs);
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "MISSING";
        var upper = status.Trim().ToUpperInvariant();
        return upper switch
        {
            "APPROVED" or "VERIFIED" => "APPROVED",
            "REJECTED" => "REJECTED",
            "EXPIRED" => "EXPIRED",
            "PENDING" or "SUBMITTED" or "IN_REVIEW" => "PENDING",
            _ => "MISSING"
        };
    }
}
