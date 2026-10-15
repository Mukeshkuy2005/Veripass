using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Compliance;

public record CreateComplianceRuleCommand(
    string Code,
    string Name,
    string Category,
    ComplianceSeverity Severity,
    string Description,
    string? RuleExpressionJson) : IRequest<Result<Guid>>;

public class CreateComplianceRuleCommandHandler : IRequestHandler<CreateComplianceRuleCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateComplianceRuleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateComplianceRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new ComplianceRule
        {
            Code = request.Code,
            Name = request.Name,
            Category = request.Category,
            Severity = request.Severity,
            Description = request.Description,
            RuleExpressionJson = request.RuleExpressionJson,
            IsActive = true
        };

        _context.ComplianceRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(rule.Id);
    }
}

public record GetComplianceDashboardQuery(Guid BusinessId) : IRequest<Result<ComplianceDashboardDto>>;

public record ComplianceDashboardDto(
    int TotalEvents,
    int PendingActions,
    int Expiring30Days,
    List<ComplianceEventItemDto> RecentEvents);

public record ComplianceEventItemDto(
    Guid Id,
    string EventType,
    string Severity,
    string Title,
    string Description,
    DateTime? DueDate,
    bool IsAcknowledged);

public class GetComplianceDashboardQueryHandler : IRequestHandler<GetComplianceDashboardQuery, Result<ComplianceDashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetComplianceDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ComplianceDashboardDto>> Handle(GetComplianceDashboardQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.ComplianceEvents
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new ComplianceEventItemDto(
                x.Id,
                x.EventType,
                x.Severity.ToString(),
                x.Title,
                x.Description,
                x.DueDate,
                x.IsAcknowledged))
            .ToListAsync(cancellationToken);

        var total = await _context.ComplianceEvents.CountAsync(x => x.BusinessId == request.BusinessId, cancellationToken);
        var pending = await _context.ComplianceEvents.CountAsync(x => x.BusinessId == request.BusinessId && !x.IsAcknowledged, cancellationToken);
        var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
        var expiring30 = await _context.ComplianceEvents.CountAsync(x => x.BusinessId == request.BusinessId && x.DueDate <= thirtyDaysFromNow, cancellationToken);

        return Result<ComplianceDashboardDto>.Success(new ComplianceDashboardDto(total, pending, expiring30, events));
    }
}

public record ComplianceCalendarEventDto(
    Guid Id,
    string Title,
    string EventType,
    string Severity,
    DateTime DueDate,
    bool IsCompleted);

public record GetComplianceCalendarQuery(Guid BusinessId, int? Month = null, int? Year = null) : IRequest<Result<List<ComplianceCalendarEventDto>>>;

public class GetComplianceCalendarQueryHandler : IRequestHandler<GetComplianceCalendarQuery, Result<List<ComplianceCalendarEventDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetComplianceCalendarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ComplianceCalendarEventDto>>> Handle(GetComplianceCalendarQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ComplianceEvents
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId && x.DueDate.HasValue);

        if (request.Year.HasValue)
        {
            query = query.Where(x => x.DueDate!.Value.Year == request.Year.Value);
        }
        if (request.Month.HasValue)
        {
            query = query.Where(x => x.DueDate!.Value.Month == request.Month.Value);
        }

        var list = await query
            .OrderBy(x => x.DueDate)
            .Select(x => new ComplianceCalendarEventDto(
                x.Id,
                x.Title,
                x.EventType,
                x.Severity.ToString(),
                x.DueDate!.Value,
                x.ResolvedAt.HasValue || x.IsAcknowledged))
            .ToListAsync(cancellationToken);

        return Result<List<ComplianceCalendarEventDto>>.Success(list);
    }
}
