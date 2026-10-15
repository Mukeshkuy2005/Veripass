using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class VerificationRequest : AuditableEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public Guid SupplierBusinessId { get; set; }
    public Business SupplierBusiness { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public string RequestType { get; set; } = "CREDENTIAL_UPDATE"; // CREDENTIAL_UPDATE, MISSING_DOCUMENT, RE_VERIFY, AUDIT
    public string? RequestedItemsJson { get; set; } // JSONB array of requested credential codes: ["GST", "ISO_9001", "INSURANCE"]
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.PENDING;
    public string? SupplierResponse { get; set; }
    public DateTime? RespondedAt { get; set; }
}
