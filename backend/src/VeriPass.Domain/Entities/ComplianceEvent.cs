using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class ComplianceEvent : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid? CredentialId { get; set; }
    public Credential? Credential { get; set; }

    public string EventType { get; set; } = string.Empty; // DOCUMENT_EXPIRING, DOCUMENT_EXPIRED, VERIFICATION_FAILED, COMPLIANCE_CHANGED, REVERIFICATION_REQUIRED
    public ComplianceSeverity Severity { get; set; } = ComplianceSeverity.MEDIUM;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
