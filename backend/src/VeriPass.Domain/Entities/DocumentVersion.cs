using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
