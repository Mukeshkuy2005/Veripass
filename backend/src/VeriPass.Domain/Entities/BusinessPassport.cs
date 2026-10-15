using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class BusinessPassport : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string PassportNumber { get; set; } = string.Empty; // e.g. "VP-IN-2026-89412"
    public string PublicIdentifier { get; set; } = string.Empty; // URL-safe slug e.g. "abc-manufacturing-89412"
    public string Version { get; set; } = "1.0";
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, SUSPENDED, EXPIRED, DRAFT
    public int VerificationScore { get; set; } = 0;
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string? QrTokenHash { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? PrivacyPreferences { get; set; }

    // Navigation
    public ICollection<PassportShare> Shares { get; set; } = new List<PassportShare>();
    public ICollection<PassportAccessLog> AccessLogs { get; set; } = new List<PassportAccessLog>();
}
