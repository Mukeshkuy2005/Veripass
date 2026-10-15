using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.Infrastructure.Notifications;

public class EmailNotificationService : IEmailService
{
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly IConfiguration _configuration;

    public EmailNotificationService(ILogger<EmailNotificationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        var smtpHost = _configuration["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
        var portStr = _configuration["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
        int.TryParse(portStr, out var smtpPort);
        if (smtpPort == 0) smtpPort = 587;

        var smtpUser = _configuration["Smtp:UserName"] ?? Environment.GetEnvironmentVariable("SMTP_USER") ?? Environment.GetEnvironmentVariable("GMAIL_USER");
        var smtpPass = _configuration["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");
        var fromEmail = _configuration["Smtp:FromEmail"] ?? (string.IsNullOrWhiteSpace(smtpUser) ? "noreply@veripass.in" : smtpUser);
        var fromName = _configuration["Smtp:FromName"] ?? "VERIPASS Sovereign Identity";

        _logger.LogInformation("📧 [EMAIL DISPATCH] Recipient: {ToEmail} | Subject: {Subject} | Target Server: {Host}:{Port}", toEmail, subject, smtpHost, smtpPort);

        if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUser.Trim(), smtpPass.Trim()),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 12000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("✅ [EMAIL SUCCESS] Live email successfully transmitted to {ToEmail} via {Host}", toEmail, smtpHost);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [EMAIL WARNING] Live SMTP transmission to {ToEmail} encountered an error. Falling back to local logging. Ensure Gmail 2FA App Password is set.", toEmail);
            }
        }
        else
        {
            _logger.LogInformation("ℹ️ [EMAIL MOCK] No SMTP credentials provided (Smtp:UserName / Smtp:Password or GMAIL_APP_PASSWORD). Simulated delivery for {ToEmail}.", toEmail);
        }
    }
}
