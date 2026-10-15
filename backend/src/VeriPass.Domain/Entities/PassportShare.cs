using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class PassportShare : BaseEntity
{
    public Guid PassportId { get; set; }
    public BusinessPassport Passport { get; set; } = null!;

    public Guid SharedByUserId { get; set; }
    public string ShareTokenHash { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? AccessScopeJson { get; set; } // JSONB: {"identity": true, "gst": true, "pan": false, "bank": false, "certifications": true}
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => RevokedAt == null && (ExpiresAt == null || DateTime.UtcNow < ExpiresAt);
}
