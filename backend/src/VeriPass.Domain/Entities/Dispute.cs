using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Dispute : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid? VerificationCaseId { get; set; }
    public VerificationCase? VerificationCase { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedAdminId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.OPEN;
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<DisputeMessage> Messages { get; set; } = new List<DisputeMessage>();
}
