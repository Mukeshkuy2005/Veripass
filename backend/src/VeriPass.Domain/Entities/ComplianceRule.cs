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
}
