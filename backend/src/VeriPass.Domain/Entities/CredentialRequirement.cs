using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class CredentialRequirement : AuditableEntity
{
    public Guid ProfileId { get; set; }
    public BuyerVerificationProfile Profile { get; set; } = null!;

    /// <summary>
    /// Type/code of credential, e.g. "GST", "PAN", "BANK_ACCOUNT", "ISO_9001", "UDYAM", "CIN"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public Guid? CredentialTypeId { get; set; }
    public CredentialType? CredentialType { get; set; }

    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Weight percentage (1 to 100).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Rules JSON or rule codes (e.g. "NAME_MATCH", "ACTIVE_REGISTRY")
    /// </summary>
    public string? Rules { get; set; }
}
