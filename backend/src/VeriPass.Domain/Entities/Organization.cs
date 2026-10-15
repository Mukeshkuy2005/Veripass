using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public OrganizationType Type { get; set; } = OrganizationType.SUPPLIER;
    public Guid? IndustryId { get; set; }
    public string? Website { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Business? Business { get; set; }
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<BuyerSupplierRelationship> BuyerRelationships { get; set; } = new List<BuyerSupplierRelationship>();
    public ICollection<SupplierShortlist> Shortlists { get; set; } = new List<SupplierShortlist>();
    public ICollection<VerificationRequest> SentVerificationRequests { get; set; } = new List<VerificationRequest>();
    public ICollection<Invitation> SentInvitations { get; set; } = new List<Invitation>();
    public ICollection<OnboardingRequest> BuyerOnboardingRequests { get; set; } = new List<OnboardingRequest>();
}
