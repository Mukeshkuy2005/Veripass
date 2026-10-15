using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Events;

public class VerificationCompletedEvent : DomainEvent
{
    public Guid VerificationCaseId { get; }
    public Guid BusinessId { get; }
    public VerificationDecision Decision { get; }

    public VerificationCompletedEvent(Guid verificationCaseId, Guid businessId, VerificationDecision decision)
    {
        VerificationCaseId = verificationCaseId;
        BusinessId = businessId;
        Decision = decision;
    }
}
