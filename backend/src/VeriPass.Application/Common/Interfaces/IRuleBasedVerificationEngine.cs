using VeriPass.Domain.Entities;

namespace VeriPass.Application.Common.Interfaces;

public record RuleExecutionDetail(
    string RuleCode,
    string RuleName,
    string CredentialType,
    string ValidationType,
    bool Passed,
    string Status, // PASSED, FAILED, ESCALATED, NOT_CONFIGURED, SKIPPED
    string? Reason,
    DateTime ExecutedAt,
    string Severity);

public record CredentialVerificationResult(
    string CredentialType,
    string FinalStatus, // APPROVED, REJECTED, ESCALATED, PENDING, MISSING
    bool RuleFailed,
    string? RuleFailedReason,
    bool HasDefinitiveFailure,
    bool RequiresManualReview,
    IReadOnlyList<RuleExecutionDetail> RuleDetails);

public interface IRuleBasedVerificationEngine
{
    /// <summary>
    /// Evaluates a single credential against applicable buyer-configured or platform default rules.
    /// Follows the pipeline: Availability -> Format -> Structure -> Checksum -> Profile/Data Match -> External Status.
    /// </summary>
    CredentialVerificationResult EvaluateCredential(
        Business supplier,
        string credentialType,
        bool isRequired,
        string? configuredRules = null,
        IReadOnlyList<ComplianceRule>? adminRules = null);

    /// <summary>
    /// Evaluates all supplier credentials according to the given requirements.
    /// </summary>
    IReadOnlyList<CredentialVerificationResult> EvaluateAll(
        Business supplier,
        IEnumerable<(string Type, bool IsRequired, string? Rules)> requirements,
        IReadOnlyList<ComplianceRule>? adminRules = null);
}
