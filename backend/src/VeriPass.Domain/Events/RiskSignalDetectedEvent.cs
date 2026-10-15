using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Events;

public class RiskSignalDetectedEvent : DomainEvent
{
    public Guid BusinessId { get; }
    public string SignalType { get; }
    public RiskLevel Severity { get; }

    public RiskSignalDetectedEvent(Guid businessId, string signalType, RiskLevel severity)
    {
        BusinessId = businessId;
        SignalType = signalType;
        Severity = severity;
    }
}
