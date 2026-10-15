using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class BuyerSupplierRelationship : AuditableEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public Guid SupplierBusinessId { get; set; }
    public Business SupplierBusiness { get; set; } = null!;

    public RelationshipStatus Status { get; set; } = RelationshipStatus.PENDING;
    public string RelationshipType { get; set; } = "DIRECT_VENDOR"; // DIRECT_VENDOR, CONTRACTOR, DISTRIBUTOR
    public RiskLevel RiskLevel { get; set; } = RiskLevel.LOW;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
}
