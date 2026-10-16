namespace VeriPass.Domain.Common;

using VeriPass.Domain.Enums;

/// <summary>
/// Authoritative domain policy for Document Scoring Contribution and Verification State Evaluation.
/// Enforces Option B (spec section 11):
/// Each unique statutory document/credential type contributes once.
/// Duplicate uploads or duplicate approvals of the same type do not artificially inflate scores.
/// </summary>
public static class DocumentScoringPolicy
{
    public const int PointsPerVerifiedStatutoryType = 25;
    public const int MaxVerificationScore = 100;
    public const int BaselineRequiredStatutoryCount = 2; // Core statutory compliance baseline (GST + PAN)

    /// <summary>
    /// Normalizes a document type or verification type into a canonical compliance credential type.
    /// </summary>
    public static string NormalizeType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return "OTHER";
        }

        var upper = rawType.Trim().ToUpperInvariant();

        if (upper.Contains("GST"))
            return "GST";

        if (upper.Contains("PAN"))
            return "PAN";

        if (upper.Contains("UDYAM") || upper.Contains("MSME") || upper.Contains("CIN"))
            return "MSME";

        if (upper.Contains("BANK") || upper.Contains("CHEQUE") || upper.Contains("ACCOUNT"))
            return "BANK";

        if (upper.Contains("ADDRESS") || upper.Contains("UTILITY") || upper.Contains("PREMISES") || upper.Contains("LEASE"))
            return "ADDRESS";

        if (upper.Contains("ISO") || upper.Contains("CERTIFICATION") || upper.Contains("QUALITY"))
            return "ISO";

        return upper;
    }

    /// <summary>
    /// Checks whether a document status represents an approved contribution.
    /// Only APPROVED contributes. PENDING, PROCESSED, REJECTED, FAILED do not.
    /// </summary>
    public static bool IsDocumentApproved(DocumentStatus status)
    {
        return status == DocumentStatus.APPROVED;
    }

    /// <summary>
    /// Checks whether a verification decision/status represents an approved contribution.
    /// </summary>
    public static bool IsVerificationApproved(VerificationDecision decision, VerificationStatus status)
    {
        return decision == VerificationDecision.APPROVED || status == VerificationStatus.VERIFIED;
    }

    /// <summary>
    /// Returns true if the canonical type maps directly to a statutory identity credential.
    /// </summary>
    public static bool IsStatutoryIdentityType(string canonicalType)
    {
        return canonicalType is "GST" or "PAN" or "MSME" or "ADDRESS";
    }
}
