using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationType Type { get; set; } = NotificationType.SYSTEM;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ResourceType { get; set; } // "Credential", "VerificationCase", "TrustScore", "Passport"
    public string? ResourceId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
