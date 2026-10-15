using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.VerificationRequests;

public record CreateVerificationRequestCommand(
    Guid BuyerOrganizationId,
    Guid SupplierBusinessId,
    Guid RequestedByUserId,
    string RequestType,
    string? RequestedItemsJson,
    string? Reason) : IRequest<Result<Guid>>;

public class CreateVerificationRequestCommandHandler : IRequestHandler<CreateVerificationRequestCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateVerificationRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateVerificationRequestCommand request, CancellationToken cancellationToken)
    {
        var req = new VerificationRequest
        {
            BuyerOrganizationId = request.BuyerOrganizationId,
            SupplierBusinessId = request.SupplierBusinessId,
            RequestedByUserId = request.RequestedByUserId,
            RequestType = request.RequestType,
            RequestedItemsJson = request.RequestedItemsJson,
            Reason = request.Reason,
            Status = RequestStatus.PENDING
        };

        _context.VerificationRequests.Add(req);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(req.Id);
    }
}

public record GetVerificationRequestsQuery(Guid? BuyerOrgId = null, Guid? SupplierBusinessId = null) 
    : IRequest<Result<List<VerificationRequestDto>>>;

public record VerificationRequestDto(
    Guid Id,
    Guid BuyerOrganizationId,
    Guid SupplierBusinessId,
    string RequestType,
    string Status,
    string? Reason,
    DateTime CreatedAt);

public class GetVerificationRequestsQueryHandler : IRequestHandler<GetVerificationRequestsQuery, Result<List<VerificationRequestDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetVerificationRequestsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<VerificationRequestDto>>> Handle(GetVerificationRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VerificationRequests.AsNoTracking().AsQueryable();

        if (request.BuyerOrgId.HasValue)
            query = query.Where(x => x.BuyerOrganizationId == request.BuyerOrgId.Value);

        if (request.SupplierBusinessId.HasValue)
            query = query.Where(x => x.SupplierBusinessId == request.SupplierBusinessId.Value || x.SupplierBusiness.OrganizationId == request.SupplierBusinessId.Value);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new VerificationRequestDto(
                x.Id,
                x.BuyerOrganizationId,
                x.SupplierBusinessId,
                x.RequestType,
                x.Status.ToString(),
                x.Reason,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<VerificationRequestDto>>.Success(list);
    }
}
