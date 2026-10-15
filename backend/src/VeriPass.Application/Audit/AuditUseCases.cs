using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.Audit;

public record GetAuditLogsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<Result<List<AuditLogDto>>>;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    Guid? OrganizationId,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? IpAddress,
    string? CorrelationId,
    DateTime CreatedAt);

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<List<AuditLogDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserId,
                x.OrganizationId,
                x.Action,
                x.ResourceType,
                x.ResourceId,
                x.IpAddress,
                x.CorrelationId,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AuditLogDto>>.Success(logs);
    }
}
