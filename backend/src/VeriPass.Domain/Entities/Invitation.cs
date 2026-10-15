using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Invitation : BaseEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public Guid? SupplierBusinessId { get; set; }
    public Business? SupplierBusiness { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; } = InvitationStatus.PENDING;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
