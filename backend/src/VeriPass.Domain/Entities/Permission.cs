using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "business.read", "verification.approve"
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty; // e.g. "business", "credential", "verification"

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
