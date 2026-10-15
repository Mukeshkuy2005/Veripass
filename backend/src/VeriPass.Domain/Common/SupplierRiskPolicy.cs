namespace VeriPass.Domain.Common;

using VeriPass.Domain.Enums;

/// <summary>
/// Authoritative domain policy for Supplier Risk Classification and Buyer Selection Eligibility.
/// Business rules:
/// Higher supplier score = safer / more suitable supplier.
/// Lower supplier score = higher risk.
///
/// Classification:
/// 0 &lt;= score &lt; 35      → HIGH RISK
/// 35 &lt;= score &lt;= 70   → MEDIUM RISK
/// 70 &lt; score &lt;= 100    → LOW RISK
///
/// Buyer Supplier Selection Eligibility:
/// Only suppliers with score &gt; 50 are eligible to appear in buyer supplier selection.
/// </summary>
public static class SupplierRiskPolicy
{
    public const double HighRiskUpperExclusive = 35.0;
    public const double MediumRiskUpperInclusive = 70.0;
    public const double BuyerEligibilityThreshold = 50.0;

    /// <summary>
    /// Evaluates the authoritative RiskLevel based on supplier score (0 to 100).
    /// Strictly enforces:
    /// Score &lt; 35       → HIGH
    /// 35 &lt;= Score &lt;= 70 → MEDIUM
    /// Score &gt; 70       → LOW
    /// Throws ArgumentOutOfRangeException if value is not in [0, 100] or is NaN.
    /// </summary>
    public static RiskLevel DetermineRiskLevel(double score)
    {
        if (double.IsNaN(score) || score < 0.0 || score > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Supplier score must be between 0 and 100.");
        }

        if (score < HighRiskUpperExclusive)
        {
            return RiskLevel.HIGH;
        }

        if (score <= MediumRiskUpperInclusive)
        {
            return RiskLevel.MEDIUM;
        }

        return RiskLevel.LOW;
    }

    /// <summary>
    /// Safe overload for nullable or unvalidated scores.
    /// Null / NaN receives HIGH risk.
    /// Clamps out-of-range values to [0, 100].
    /// </summary>
    public static RiskLevel DetermineRiskLevelSafe(double? score)
    {
        if (!score.HasValue || double.IsNaN(score.Value))
        {
            return RiskLevel.HIGH;
        }

        var clamped = Math.Clamp(score.Value, 0.0, 100.0);
        return DetermineRiskLevel(clamped);
    }

    /// <summary>
    /// Returns the authoritative RiskLevel string ("HIGH", "MEDIUM", "LOW").
    /// </summary>
    public static string GetRiskLevelString(double score)
    {
        return DetermineRiskLevel(score).ToString();
    }

    /// <summary>
    /// Returns the authoritative RiskLevel string safely ("HIGH", "MEDIUM", "LOW").
    /// </summary>
    public static string GetRiskLevelStringSafe(double? score)
    {
        return DetermineRiskLevelSafe(score).ToString();
    }

    /// <summary>
    /// Authoritative Buyer Selection Eligibility check.
    /// Strictly requires score &gt; 50.
    /// </summary>
    public static bool IsEligibleForBuyer(double score)
    {
        if (double.IsNaN(score) || score < 0.0 || score > 100.0)
        {
            return false;
        }

        return score > BuyerEligibilityThreshold;
    }

    /// <summary>
    /// Safe overload for nullable or unvalidated buyer eligibility checks.
    /// </summary>
    public static bool IsEligibleForBuyerSafe(double? score)
    {
        if (!score.HasValue || double.IsNaN(score.Value))
        {
            return false;
        }

        return IsEligibleForBuyer(score.Value);
    }
}
