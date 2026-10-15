using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class VerificationCheck : BaseEntity
{
    public Guid VerificationCaseId { get; set; }
    public VerificationCase VerificationCase { get; set; } = null!;

    public string Provider { get; set; } = string.Empty; // GSTN, NSDL_PAN, MCA21, UDYAM, NPCI_PENNY_DROP, OCR_ENGINE
    public string CheckType { get; set; } = string.Empty; // REGISTRY_LOOKUP, OCR_EXTRACTION, NAME_MATCH, TAMPER_CHECK, ACTIVE_STATUS
    public VerificationStatus Status { get; set; } = VerificationStatus.PENDING;
    public string? RequestReference { get; set; }
    public string? ResultJson { get; set; } // JSONB
    public decimal? ConfidenceScore { get; set; } // 0.00 to 100.00
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
