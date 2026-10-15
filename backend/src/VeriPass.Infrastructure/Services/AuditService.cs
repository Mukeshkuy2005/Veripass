using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;

namespace VeriPass.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AuditLog> RecordAsync(RecordAuditOptions options, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        var effectiveUserId = options.OverrideUserId ?? _currentUserService.UserId;
        var effectiveOrgId = options.OverrideOrganizationId ?? _currentUserService.OrganizationId;

        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        if (httpContext != null && httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            ipAddress = forwardedFor.ToString().Split(',')[0].Trim();
        }

        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "VeriPass Enterprise Client";
        if (userAgent.Length > 500) userAgent = userAgent[..500];

        var correlationId = httpContext?.TraceIdentifier 
                            ?? (httpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var corr) == true ? corr.ToString() : Guid.NewGuid().ToString("N"));

        var eventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = effectiveUserId,
            OrganizationId = effectiveOrgId,
            Action = options.Action.Trim().ToUpperInvariant(),
            Category = options.Category.Trim().ToUpperInvariant(),
            ResourceType = options.TargetType.Trim().ToUpperInvariant(),
            ResourceId = options.TargetId,
            TargetName = options.TargetName,
            Result = options.Result.Trim().ToUpperInvariant(),
            EventCode = eventCode,
            OldValuesJson = options.OldValuesJson,
            NewValuesJson = options.NewValuesJson,
            MetadataJson = options.MetadataJson,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log entry {Action} ({EventCode})", options.Action, eventCode);
        }

        return auditLog;
    }
}
