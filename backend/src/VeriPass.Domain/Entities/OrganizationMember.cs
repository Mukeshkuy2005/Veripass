using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class OrganizationMember : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string Status { get; set; } = "ACTIVE";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
