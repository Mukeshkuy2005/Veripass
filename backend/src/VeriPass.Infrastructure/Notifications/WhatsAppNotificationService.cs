using Microsoft.Extensions.Logging;

namespace VeriPass.Infrastructure.Notifications;

public interface IWhatsAppNotificationService
{
    Task SendWhatsAppMessageAsync(string phoneNumber, string templateName, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

public class WhatsAppNotificationService : IWhatsAppNotificationService
{
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(ILogger<WhatsAppNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendWhatsAppMessageAsync(string phoneNumber, string templateName, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dispatching WhatsApp Template [{Template}] to {Phone}", templateName, phoneNumber);
        return Task.CompletedTask;
    }
}
