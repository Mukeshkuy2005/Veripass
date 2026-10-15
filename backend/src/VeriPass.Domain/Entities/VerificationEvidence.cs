using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class VerificationEvidence : BaseEntity
{
    public Guid VerificationCaseId { get; set; }
    public VerificationCase VerificationCase { get; set; } = null!;

    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    public string EvidenceType { get; set; } = "FIELD_MATCH"; // FIELD_MATCH, SIGNATURE_MATCH, TAMPER_DETECTION, REGISTRY_CONFIRMATION
    public string FieldName { get; set; } = string.Empty; // e.g. "LegalName", "Pan", "Gstin", "Ifsc"
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string MatchStatus { get; set; } = "MATCH"; // MATCH, PARTIAL_MATCH, MISMATCH, NOT_FOUND
    public decimal? ConfidenceScore { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
