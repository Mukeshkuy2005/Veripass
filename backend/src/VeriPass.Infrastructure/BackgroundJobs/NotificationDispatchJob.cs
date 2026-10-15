using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class NotificationDispatchJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationDispatchJob> _logger;

    public NotificationDispatchJob(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<NotificationDispatchJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Notification Dispatch Job...");

        // Find unread critical/statutory notifications created within the last 30 minutes
        var recentNotifications = await _context.Notifications
            .Include(n => n.User)
            .Where(n => !n.IsRead && n.CreatedAt >= DateTime.UtcNow.AddMinutes(-30))
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var notification in recentNotifications)
        {
            if (notification.User != null && !string.IsNullOrEmpty(notification.User.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        notification.User.Email,
                        $"[VERIPASS Alert] {notification.Title}",
                        $"<p>Dear {notification.User.FirstName},</p><p>{notification.Message}</p>",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch email notification {NotificationId}", notification.Id);
                }
            }
        }

        _logger.LogInformation("Notification Dispatch Job evaluated {Count} notifications.", recentNotifications.Count);
    }
}
