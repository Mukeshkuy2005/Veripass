using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.Notifications;

public record GetNotificationsQuery(Guid UserId) : IRequest<Result<List<NotificationDto>>>;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? ResourceType,
    string? ResourceId,
    bool IsRead,
    DateTime CreatedAt);

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, Result<List<NotificationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new NotificationDto(
                x.Id,
                x.Type.ToString(),
                x.Title,
                x.Message,
                x.ResourceType,
                x.ResourceId,
                x.IsRead,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<NotificationDto>>.Success(list);
    }
}

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<Result>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notif = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == request.NotificationId, cancellationToken);
        if (notif == null)
            return Result.Failure("Notification not found.");

        notif.IsRead = true;
        notif.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public record MarkAllNotificationsAsReadCommand(Guid UserId) : IRequest<Result>;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var unread = await _context.Notifications
            .Where(x => x.UserId == request.UserId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

