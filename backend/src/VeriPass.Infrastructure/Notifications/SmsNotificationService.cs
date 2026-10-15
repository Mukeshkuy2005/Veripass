using Microsoft.Extensions.Logging;

namespace VeriPass.Infrastructure.Notifications;

public interface ISmsNotificationService
{
    Task SendSmsAsync(string phoneNumber, string message, string dltTemplateId, CancellationToken cancellationToken = default);
}

public class SmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<SmsNotificationService> _logger;

    public SmsNotificationService(ILogger<SmsNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string phoneNumber, string message, string dltTemplateId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dispatching DLT SMS [{TemplateId}] to {Phone}: {Message}", dltTemplateId, phoneNumber, message);
        return Task.CompletedTask;
    }
}
