using VeriPass.Domain.Enums;

namespace VeriPass.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default);
    Task BroadcastOrganizationNotificationAsync(Guid organizationId, NotificationType type, string title, string message, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default);
}
