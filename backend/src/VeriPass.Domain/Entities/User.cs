using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool EmailVerified { get; set; } = false;
    public bool PhoneVerified { get; set; } = false;
    public bool MfaEnabled { get; set; } = false;
    public string MfaPreferredMethod { get; set; } = "EMAIL";
    public string? MfaOtpCode { get; set; }
    public DateTime? MfaOtpExpiresAt { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public string? NotificationPreferences { get; set; }
    public string? AssignedScopeJson { get; set; }

    // Navigation
    public ICollection<OrganizationMember> Memberships { get; set; } = new List<OrganizationMember>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
