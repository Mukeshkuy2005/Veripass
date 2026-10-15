using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class CredentialType : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "GST", "PAN", "CIN", "UDYAM", "ISO_9001"
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "STATUTORY"; // STATUTORY, FINANCIAL, QUALITY, SAFETY, TAX
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = false;
    public bool RequiresExpiry { get; set; } = false;
    public string? VerificationProvider { get; set; } // e.g. "GSTN", "NSDL", "MCA", "UDYAM", "BANK"
    public string? SchemaJson { get; set; } // JSON Schema string for metadata validation
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();
}
