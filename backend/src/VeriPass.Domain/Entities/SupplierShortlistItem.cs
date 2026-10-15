using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class SupplierShortlistItem : BaseEntity
{
    public Guid ShortlistId { get; set; }
    public SupplierShortlist Shortlist { get; set; } = null!;

    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
