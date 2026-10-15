using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Document : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public Guid? CredentialId { get; set; }
    public Credential? Credential { get; set; }

    public string DocumentType { get; set; } = string.Empty; // GST_CERTIFICATE, PAN_CARD, UDYAM_CERT, CANCELLED_CHEQUE, ISO_CERT, etc.
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty; // S3 / Blob key
    public string MimeType { get; set; } = "application/pdf";
    public long FileSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.UPLOADED;
    public Guid? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int CurrentVersion { get; set; } = 1;
    public string? ExtractedDataJson { get; set; } // JSONB: OCR key-value output
    public decimal? ExtractionConfidence { get; set; } // 0.00 to 100.00%

    // Navigation
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<VerificationEvidence> VerificationEvidences { get; set; } = new List<VerificationEvidence>();
}
