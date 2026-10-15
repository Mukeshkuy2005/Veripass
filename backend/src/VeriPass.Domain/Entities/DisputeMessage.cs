using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class DisputeMessage : BaseEntity
{
    public Guid DisputeId { get; set; }
    public Dispute Dispute { get; set; } = null!;

    public Guid SenderUserId { get; set; }
    public string SenderRole { get; set; } = "SUPPLIER"; // SUPPLIER, ADMIN, VERIFIER
    public string Message { get; set; } = string.Empty;
    public string? AttachmentsJson { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
