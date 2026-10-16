using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Credentials;

public record CreateCredentialCommand(
    Guid BusinessId,
    Guid CredentialTypeId,
    string CredentialNumber,
    string? Issuer,
    DateTime? IssuedDate,
    DateTime? ExpiryDate) : IRequest<Result<Guid>>;

public class CreateCredentialCommandHandler : IRequestHandler<CreateCredentialCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public CreateCredentialCommandHandler(IApplicationDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<Result<Guid>> Handle(CreateCredentialCommand request, CancellationToken cancellationToken)
    {
        var business = await _context.Businesses
            .FirstOrDefaultAsync(b => b.Id == request.BusinessId || b.OrganizationId == request.BusinessId, cancellationToken);
        var actualBusinessId = business?.Id ?? request.BusinessId;

        var cred = new Credential
        {
            BusinessId = actualBusinessId,
            CredentialTypeId = request.CredentialTypeId,
            CredentialNumberHash = _tokenService.HashToken(request.CredentialNumber),
            CredentialDisplayValue = request.CredentialNumber.Length > 4 
                ? $"••••{request.CredentialNumber[^4..]}" 
                : request.CredentialNumber,
            Issuer = request.Issuer,
            IssuedDate = request.IssuedDate,
            ExpiryDate = request.ExpiryDate,
            Status = CredentialStatus.SUBMITTED,
            VerificationStatus = VerificationStatus.PENDING
        };

        _context.Credentials.Add(cred);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(cred.Id);
    }
}

public record GetCredentialsQuery(Guid BusinessId) : IRequest<Result<List<CredentialDto>>>;

public record CredentialDto(
    Guid Id,
    Guid CredentialTypeId,
    string? DisplayValue,
    string? Issuer,
    string Status,
    string VerificationStatus,
    DateTime? ExpiryDate,
    DateTime? LastVerifiedAt);

public class GetCredentialsQueryHandler : IRequestHandler<GetCredentialsQuery, Result<List<CredentialDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCredentialsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CredentialDto>>> Handle(GetCredentialsQuery request, CancellationToken cancellationToken)
    {
        var creds = await _context.Credentials
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId || x.Business.OrganizationId == request.BusinessId)
            .Select(x => new CredentialDto(
                x.Id,
                x.CredentialTypeId,
                x.CredentialDisplayValue,
                x.Issuer,
                x.Status.ToString(),
                x.VerificationStatus.ToString(),
                x.ExpiryDate,
                x.LastVerifiedAt))
            .ToListAsync(cancellationToken);

        return Result<List<CredentialDto>>.Success(creds);
    }
}

public record GetCredentialQuery(Guid Id) : IRequest<Result<CredentialDto>>;

public class GetCredentialQueryHandler : IRequestHandler<GetCredentialQuery, Result<CredentialDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCredentialQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CredentialDto>> Handle(GetCredentialQuery request, CancellationToken cancellationToken)
    {
        var x = await _context.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (x == null)
            return Result<CredentialDto>.Failure("Credential not found.");

        return Result<CredentialDto>.Success(new CredentialDto(
            x.Id,
            x.CredentialTypeId,
            x.CredentialDisplayValue,
            x.Issuer,
            x.Status.ToString(),
            x.VerificationStatus.ToString(),
            x.ExpiryDate,
            x.LastVerifiedAt));
    }
}

public record UpdateCredentialCommand(
    Guid CredentialId,
    string? Issuer,
    DateTime? IssuedDate,
    DateTime? ExpiryDate) : IRequest<Result<bool>>;

public class UpdateCredentialCommandHandler : IRequestHandler<UpdateCredentialCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCredentialCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateCredentialCommand request, CancellationToken cancellationToken)
    {
        var cred = await _context.Credentials
            .FirstOrDefaultAsync(c => c.Id == request.CredentialId, cancellationToken);

        if (cred == null)
            return Result<bool>.Failure("Credential not found.");

        cred.Issuer = request.Issuer ?? cred.Issuer;
        cred.IssuedDate = request.IssuedDate ?? cred.IssuedDate;
        cred.ExpiryDate = request.ExpiryDate ?? cred.ExpiryDate;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record DeleteCredentialCommand(Guid CredentialId) : IRequest<Result<bool>>;

public class DeleteCredentialCommandHandler : IRequestHandler<DeleteCredentialCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteCredentialCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteCredentialCommand request, CancellationToken cancellationToken)
    {
        var cred = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == request.CredentialId, cancellationToken);
        if (cred == null) return Result<bool>.Failure("Credential not found.");

        _context.Credentials.Remove(cred);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record SubmitCredentialCommand(Guid CredentialId) : IRequest<Result<bool>>;

public class SubmitCredentialCommandHandler : IRequestHandler<SubmitCredentialCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public SubmitCredentialCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(SubmitCredentialCommand request, CancellationToken cancellationToken)
    {
        var cred = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == request.CredentialId, cancellationToken);
        if (cred == null) return Result<bool>.Failure("Credential not found.");

        cred.Status = CredentialStatus.SUBMITTED;
        cred.VerificationStatus = VerificationStatus.PENDING;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record VerifyCredentialCommand(Guid CredentialId) : IRequest<Result<bool>>;

public class VerifyCredentialCommandHandler : IRequestHandler<VerifyCredentialCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITrustScoreCalculator _trustScoreCalculator;

    public VerifyCredentialCommandHandler(IApplicationDbContext context, ITrustScoreCalculator trustScoreCalculator)
    {
        _context = context;
        _trustScoreCalculator = trustScoreCalculator;
    }

    public async Task<Result<bool>> Handle(VerifyCredentialCommand request, CancellationToken cancellationToken)
    {
        var cred = await _context.Credentials.FirstOrDefaultAsync(c => c.Id == request.CredentialId, cancellationToken);
        if (cred == null) return Result<bool>.Failure("Credential not found.");

        cred.Status = CredentialStatus.VERIFIED;
        cred.VerificationStatus = VerificationStatus.VERIFIED;
        cred.LastVerifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _trustScoreCalculator.CalculateAsync(cred.BusinessId, cancellationToken);
        return Result<bool>.Success(true);
    }
}


