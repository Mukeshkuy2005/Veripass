using VeriPass.Domain.Common;

namespace VeriPass.Domain.Events;

public class DocumentUploadedEvent : DomainEvent
{
    public Guid DocumentId { get; }
    public Guid BusinessId { get; }
    public string DocumentType { get; }
    public string StorageKey { get; }

    public DocumentUploadedEvent(Guid documentId, Guid businessId, string documentType, string storageKey)
    {
        DocumentId = documentId;
        BusinessId = businessId;
        DocumentType = documentType;
        StorageKey = storageKey;
    }
}
