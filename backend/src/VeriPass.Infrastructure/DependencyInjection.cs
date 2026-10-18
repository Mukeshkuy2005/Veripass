using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Infrastructure.BackgroundJobs;
using VeriPass.Infrastructure.Caching;
using VeriPass.Infrastructure.DocumentIntelligence;
using VeriPass.Infrastructure.Identity;
using VeriPass.Infrastructure.Messaging;
using VeriPass.Infrastructure.Notifications;
using VeriPass.Infrastructure.Persistence;
using VeriPass.Infrastructure.Persistence.Interceptors;
using VeriPass.Infrastructure.Search;
using VeriPass.Infrastructure.Storage;
using VeriPass.Infrastructure.TrustScoring;
using VeriPass.Infrastructure.VerificationProviders;

namespace VeriPass.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase", false);
        if (useInMemory)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("VeriPass_Dev_Db"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=veripass_db;Username=postgres;Password=postgres";

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                });
            });
        }

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Identity & Security
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuditService, VeriPass.Infrastructure.Services.AuditService>();

        // Storage & Signed URLs
        services.AddSingleton<ISignedUrlService, SignedUrlService>();
        services.AddSingleton<IFileStorageService, S3CompatibleStorageService>();

        // Document Intelligence
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<IDocumentTamperDetectionService, DocumentTamperDetectionService>();
        services.AddSingleton<IDocumentIntelligenceService, DocumentExtractionService>();

        // Verification & Scoring
        services.AddSingleton<IVerificationProviderFactory, VerificationProviderFactory>();
        services.AddScoped<ITrustScoreCalculator, TrustScoreCalculator>();
        services.AddScoped<IRuleBasedVerificationEngine, RuleBasedVerificationEngine>();
        services.AddScoped<IBuyerTrustScoreService, BuyerTrustScoreService>();

        // Notifications
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IEmailService, EmailNotificationService>();
        services.AddSingleton<IWhatsAppNotificationService, WhatsAppNotificationService>();
        services.AddSingleton<ISmsNotificationService, SmsNotificationService>();

        // Background Jobs
        services.AddScoped<DocumentExpiryJob>();
        services.AddScoped<ReVerificationJob>();
        services.AddScoped<ComplianceMonitoringJob>();
        services.AddScoped<TrustScoreRecalculationJob>();
        services.AddScoped<RiskDetectionJob>();
        services.AddScoped<NotificationDispatchJob>();
        services.AddHostedService<ContinuousVerificationWorker>();

        // Caching, Search, Messaging
        services.AddSingleton<RedisCacheService>();
        services.AddScoped<SupplierSearchService>();
        services.AddScoped<EventPublisher>();

        return services;
    }
}
