using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Credential : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid CredentialTypeId { get; set; }
    public CredentialType CredentialType { get; set; } = null!;

    public string? CredentialNumberEncrypted { get; set; }
    public string? CredentialNumberHash { get; set; } // SHA256 of plain number for de-duplication
    public string? CredentialDisplayValue { get; set; } // Masked/safe display, e.g. "27AAACW1234F1Z5"
    public string? Issuer { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public CredentialStatus Status { get; set; } = CredentialStatus.DRAFT;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.UNVERIFIED;
    public DateTime? LastVerifiedAt { get; set; }
    public DateTime? NextVerificationAt { get; set; }
    public string? MetadataJson { get; set; } // JSONB in PostgreSQL

    // Navigation
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<VerificationCase> VerificationCases { get; set; } = new List<VerificationCase>();
    public ICollection<ComplianceEvent> ComplianceEvents { get; set; } = new List<ComplianceEvent>();
}
