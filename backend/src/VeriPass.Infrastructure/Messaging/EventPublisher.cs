using MediatR;
using Microsoft.Extensions.Logging;
using VeriPass.Domain.Common;

namespace VeriPass.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
}

public class EventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IMediator mediator, ILogger<EventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        _logger.LogInformation("Publishing domain event {EventType} at {OccurredOn}", typeof(TEvent).Name, domainEvent.OccurredOn);
        if (domainEvent is INotification notification)
        {
            await _mediator.Publish(notification, cancellationToken);
        }
    }
}
