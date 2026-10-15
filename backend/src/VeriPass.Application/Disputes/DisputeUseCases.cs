using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Disputes;

public record CreateDisputeCommand(
    Guid BusinessId,
    Guid CreatedByUserId,
    string Subject,
    string Description,
    Guid? VerificationCaseId = null) : IRequest<Result<Guid>>;

public class CreateDisputeCommandHandler : IRequestHandler<CreateDisputeCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateDisputeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateDisputeCommand request, CancellationToken cancellationToken)
    {
        var dispute = new Dispute
        {
            BusinessId = request.BusinessId,
            CreatedByUserId = request.CreatedByUserId,
            Subject = request.Subject,
            Description = request.Description,
            VerificationCaseId = request.VerificationCaseId,
            Status = DisputeStatus.OPEN
        };

        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(dispute.Id);
    }
}

public record GetDisputesQuery(Guid BusinessId) : IRequest<Result<List<DisputeDto>>>;

public record DisputeDto(
    Guid Id,
    string Subject,
    string Description,
    string Status,
    string? Resolution,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, Result<List<DisputeDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDisputesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DisputeDto>>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        var disputes = await _context.Disputes
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DisputeDto(
                x.Id,
                x.Subject,
                x.Description,
                x.Status.ToString(),
                x.Resolution,
                x.CreatedAt,
                x.ResolvedAt))
            .ToListAsync(cancellationToken);

        return Result<List<DisputeDto>>.Success(disputes);
    }
}

public record GetDisputeByIdQuery(Guid DisputeId) : IRequest<Result<DisputeDto>>;

public class GetDisputeByIdQueryHandler : IRequestHandler<GetDisputeByIdQuery, Result<DisputeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDisputeByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DisputeDto>> Handle(GetDisputeByIdQuery request, CancellationToken cancellationToken)
    {
        var x = await _context.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);
        if (x == null) return Result<DisputeDto>.Failure("Dispute not found.");

        return Result<DisputeDto>.Success(new DisputeDto(
            x.Id,
            x.Subject,
            x.Description,
            x.Status.ToString(),
            x.Resolution,
            x.CreatedAt,
            x.ResolvedAt));
    }
}

public record RespondToDisputeCommand(Guid DisputeId, string ResponseText) : IRequest<Result<bool>>;

public class RespondToDisputeCommandHandler : IRequestHandler<RespondToDisputeCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public RespondToDisputeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RespondToDisputeCommand request, CancellationToken cancellationToken)
    {
        var dispute = await _context.Disputes.FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);
        if (dispute == null) return Result<bool>.Failure("Dispute not found.");

        dispute.Status = DisputeStatus.UNDER_REVIEW;
        dispute.Resolution = $"Response: {request.ResponseText}";
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record ResolveDisputeCommand(Guid DisputeId, string Resolution) : IRequest<Result<bool>>;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public ResolveDisputeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var dispute = await _context.Disputes.FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);
        if (dispute == null) return Result<bool>.Failure("Dispute not found.");

        dispute.Status = DisputeStatus.RESOLVED;
        dispute.Resolution = request.Resolution;
        dispute.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

