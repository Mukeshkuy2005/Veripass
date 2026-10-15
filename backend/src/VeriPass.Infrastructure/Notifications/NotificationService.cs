using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendNotificationAsync(
        Guid userId, NotificationType type, string title, string message, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ResourceType = resourceType,
            ResourceId = resourceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Notification [{Type}] sent to User {UserId}: {Title}", type, userId, title);
    }

    public async Task BroadcastOrganizationNotificationAsync(
        Guid organizationId, NotificationType type, string title, string message, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default)
    {
        var memberUserIds = await _context.OrganizationMembers
            .Where(m => m.OrganizationId == organizationId && m.Status == "ACTIVE")
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in memberUserIds)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ResourceType = resourceType,
                ResourceId = resourceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Broadcasted notification [{Type}] to {Count} members of Organization {OrgId}", type, memberUserIds.Count, organizationId);
    }
}
