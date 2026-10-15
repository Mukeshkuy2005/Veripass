using VeriPass.Domain.Common;

namespace VeriPass.Domain.Events;

public class BusinessCreatedEvent : DomainEvent
{
    public Guid BusinessId { get; }
    public string LegalName { get; }
    public string BusinessIdNumber { get; }

    public BusinessCreatedEvent(Guid businessId, string legalName, string businessIdNumber)
    {
        BusinessId = businessId;
        LegalName = legalName;
        BusinessIdNumber = businessIdNumber;
    }
}
