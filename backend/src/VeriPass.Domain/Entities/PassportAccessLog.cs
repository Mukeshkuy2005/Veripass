using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class PassportAccessLog : BaseEntity
{
    public Guid PassportId { get; set; }
    public BusinessPassport Passport { get; set; } = null!;

    public Guid? ViewerUserId { get; set; }
    public Guid? ViewerOrganizationId { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
}
