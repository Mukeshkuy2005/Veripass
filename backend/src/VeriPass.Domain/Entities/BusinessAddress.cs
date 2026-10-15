using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class BusinessAddress : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string AddressType { get; set; } = "REGISTERED"; // REGISTERED, PRINCIPAL, FACTORY, BRANCH
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsPrimary { get; set; } = true;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.UNVERIFIED;
}
