using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class OnboardingRequirement : BaseEntity
{
    public string Code { get; set; } = string.Empty; // GST, PAN, CIN, ISO_9001, BANK_ACCOUNT, etc.
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "STATUTORY"; // STATUTORY, FINANCIAL, QUALITY, SAFETY, TAX
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefaultMandatory { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OnboardingRequestRequirement> RequestRequirements { get; set; } = new List<OnboardingRequestRequirement>();
}
