using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Risk;

public record GetRiskSignalsQuery(Guid BusinessId) : IRequest<Result<List<RiskSignalDto>>>;

public record RiskSignalDto(
    Guid Id,
    string SignalType,
    string Severity,
    int ScoreImpact,
    string Description,
    string Status,
    DateTime DetectedAt);

public class GetRiskSignalsQueryHandler : IRequestHandler<GetRiskSignalsQuery, Result<List<RiskSignalDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRiskSignalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RiskSignalDto>>> Handle(GetRiskSignalsQuery request, CancellationToken cancellationToken)
    {
        var signals = await _context.RiskSignals
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId)
            .OrderByDescending(x => x.DetectedAt)
            .Select(x => new RiskSignalDto(
                x.Id,
                x.SignalType,
                x.Severity.ToString(),
                x.ScoreImpact,
                x.Description,
                x.Status,
                x.DetectedAt))
            .ToListAsync(cancellationToken);

        return Result<List<RiskSignalDto>>.Success(signals);
    }
}

public record ResolveRiskSignalCommand(Guid SignalId, string Notes) : IRequest<Result>;

public class ResolveRiskSignalCommandHandler : IRequestHandler<ResolveRiskSignalCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ResolveRiskSignalCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ResolveRiskSignalCommand request, CancellationToken cancellationToken)
    {
        var signal = await _context.RiskSignals.FirstOrDefaultAsync(x => x.Id == request.SignalId, cancellationToken);
        if (signal == null)
            return Result.Failure("Risk signal not found.");

        signal.Status = "RESOLVED";
        signal.ResolvedAt = DateTime.UtcNow;
        signal.ResolutionNotes = request.Notes;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record BusinessRiskOverviewDto(
    Guid BusinessId,
    int RiskScore,
    int ActiveSignalsCount,
    int ResolvedSignalsCount,
    List<RiskSignalDto> Signals);

public record GetBusinessRiskOverviewQuery(Guid BusinessId) : IRequest<Result<BusinessRiskOverviewDto>>;

public class GetBusinessRiskOverviewQueryHandler : IRequestHandler<GetBusinessRiskOverviewQuery, Result<BusinessRiskOverviewDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessRiskOverviewQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BusinessRiskOverviewDto>> Handle(GetBusinessRiskOverviewQuery request, CancellationToken cancellationToken)
    {
        var ts = await _context.TrustScores.AsNoTracking().FirstOrDefaultAsync(t => t.BusinessId == request.BusinessId, cancellationToken);
        var signals = await _context.RiskSignals
            .AsNoTracking()
            .Where(x => x.BusinessId == request.BusinessId)
            .OrderByDescending(x => x.DetectedAt)
            .Select(x => new RiskSignalDto(
                x.Id,
                x.SignalType,
                x.Severity.ToString(),
                x.ScoreImpact,
                x.Description,
                x.Status,
                x.DetectedAt))
            .ToListAsync(cancellationToken);

        var activeCount = signals.Count(s => s.Status == "ACTIVE");
        var resolvedCount = signals.Count(s => s.Status == "RESOLVED");

        return Result<BusinessRiskOverviewDto>.Success(new BusinessRiskOverviewDto(
            request.BusinessId,
            ts?.RiskScore ?? 100,
            activeCount,
            resolvedCount,
            signals));
    }
}

public record AdminRiskSignalDto(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    string SignalType,
    string Severity,
    int ScoreImpact,
    string Description,
    string Status,
    DateTime DetectedAt);

public record GetAdminRiskSignalsQuery() : IRequest<Result<List<AdminRiskSignalDto>>>;

public class GetAdminRiskSignalsQueryHandler : IRequestHandler<GetAdminRiskSignalsQuery, Result<List<AdminRiskSignalDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminRiskSignalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AdminRiskSignalDto>>> Handle(GetAdminRiskSignalsQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.RiskSignals
            .Include(x => x.Business)
            .AsNoTracking()
            .OrderByDescending(x => x.DetectedAt)
            .Take(100)
            .Select(x => new AdminRiskSignalDto(
                x.Id,
                x.BusinessId,
                x.Business.LegalName,
                x.SignalType,
                x.Severity.ToString(),
                x.ScoreImpact,
                x.Description,
                x.Status,
                x.DetectedAt))
            .ToListAsync(cancellationToken);

        return Result<List<AdminRiskSignalDto>>.Success(list);
    }
}
