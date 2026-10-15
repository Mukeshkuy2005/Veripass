using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class SupplierShortlist : AuditableEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<SupplierShortlistItem> Items { get; set; } = new List<SupplierShortlistItem>();
}
