using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VeriPass.Infrastructure.BackgroundJobs;

public class ContinuousVerificationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContinuousVerificationWorker> _logger;

    public ContinuousVerificationWorker(IServiceProvider serviceProvider, ILogger<ContinuousVerificationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VERIPASS Continuous Verification Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var expiryJob = scope.ServiceProvider.GetRequiredService<DocumentExpiryJob>();
                var reverificationJob = scope.ServiceProvider.GetRequiredService<ReVerificationJob>();
                var complianceJob = scope.ServiceProvider.GetRequiredService<ComplianceMonitoringJob>();
                var trustScoreJob = scope.ServiceProvider.GetRequiredService<TrustScoreRecalculationJob>();
                var riskDetectionJob = scope.ServiceProvider.GetRequiredService<RiskDetectionJob>();
                var dispatchJob = scope.ServiceProvider.GetRequiredService<NotificationDispatchJob>();

                await expiryJob.ExecuteAsync(stoppingToken);
                await reverificationJob.ExecuteAsync(stoppingToken);
                await complianceJob.ExecuteAsync(stoppingToken);
                await trustScoreJob.ExecuteAsync(stoppingToken);
                await riskDetectionJob.ExecuteAsync(stoppingToken);
                await dispatchJob.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during continuous verification worker cycle.");
            }

            try
            {
                // Runs every 5 minutes in background
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
