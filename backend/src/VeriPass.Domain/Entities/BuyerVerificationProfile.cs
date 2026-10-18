using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class BuyerVerificationProfile : AuditableEntity
{
    public Guid BuyerOrganizationId { get; set; }
    public Organization BuyerOrganization { get; set; } = null!;

    public string Name { get; set; } = "Standard Verification Profile";
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;

    /// <summary>
    /// Sum of weights of all requirements. Must equal 100.
    /// </summary>
    public int TotalWeight { get; set; } = 100;

    public ICollection<CredentialRequirement> Requirements { get; set; } = new List<CredentialRequirement>();

    public void ValidateWeights()
    {
        var sum = Requirements?.Sum(r => r.Weight) ?? 0;
        if (sum != 100)
        {
            throw new InvalidOperationException($"Buyer verification profile weights must sum to exactly 100. Current sum is {sum}.");
        }
        TotalWeight = sum;
    }
}
