using VeriPass.Domain.Common;

namespace VeriPass.Domain.Events;

public class CredentialExpiredEvent : DomainEvent
{
    public Guid CredentialId { get; }
    public Guid BusinessId { get; }
    public string CredentialTypeCode { get; }

    public CredentialExpiredEvent(Guid credentialId, Guid businessId, string credentialTypeCode)
    {
        CredentialId = credentialId;
        BusinessId = businessId;
        CredentialTypeCode = credentialTypeCode;
    }
}
