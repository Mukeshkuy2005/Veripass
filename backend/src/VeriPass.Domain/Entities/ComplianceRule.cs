using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class ComplianceRule : AuditableEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "RULE_GST_ACTIVE", "RULE_INSURANCE_VALIDITY", "RULE_BANK_VERIFIED"
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "STATUTORY";
    public ComplianceSeverity Severity { get; set; } = ComplianceSeverity.HIGH;
    public string Description { get; set; } = string.Empty;
    public string? RuleExpressionJson { get; set; } // Condition JSON
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Target credential type: GSTIN, PAN, UDYAM, BANK, BUSINESS_REGISTRATION
    /// </summary>
    public string? CredentialType { get; set; }

    /// <summary>
    /// Type of validation: FORMAT, STRUCTURE, CHECKSUM, DATA_MATCH, REGISTRY, STATUS
    /// </summary>
    public string? ValidationType { get; set; }

    /// <summary>
    /// Whether this credential requirement is mandatory by default in platform baseline
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Default weight percentage for Trust Score calculation (active default weights sum to 100)
    /// </summary>
    public int DefaultWeight { get; set; } = 0;
}
