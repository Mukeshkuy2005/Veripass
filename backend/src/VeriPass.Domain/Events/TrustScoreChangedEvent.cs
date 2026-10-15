using VeriPass.Domain.Common;

namespace VeriPass.Domain.Events;

public class TrustScoreChangedEvent : DomainEvent
{
    public Guid BusinessId { get; }
    public int PreviousScore { get; }
    public int NewScore { get; }
    public string Reason { get; }

    public TrustScoreChangedEvent(Guid businessId, int previousScore, int newScore, string reason)
    {
        BusinessId = businessId;
        PreviousScore = previousScore;
        NewScore = newScore;
        Reason = reason;
    }
}
