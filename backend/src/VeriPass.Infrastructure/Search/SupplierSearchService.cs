using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.Search;

public interface ISupplierSearchService
{
    Task<List<Business>> SearchAsync(string query, string? category = null, int? minScore = null, CancellationToken cancellationToken = default);
}

public class SupplierSearchService : ISupplierSearchService
{
    private readonly IApplicationDbContext _context;

    public SupplierSearchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Business>> SearchAsync(string query, string? category = null, int? minScore = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Businesses
            .Include(x => x.TrustScore)
            .Include(x => x.Addresses)
            .Include(x => x.Organization)
            .Where(x => x.Organization.Type == OrganizationType.SUPPLIER)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            q = q.Where(x => x.LegalName.ToLower().Contains(lowerQuery) ||
                             (x.TradeName != null && x.TradeName.ToLower().Contains(lowerQuery)) ||
                             (x.Gstin != null && x.Gstin.ToLower().Contains(lowerQuery)) ||
                             (x.Pan != null && x.Pan.ToLower().Contains(lowerQuery)));
        }

        if (minScore.HasValue)
        {
            q = q.Where(x => x.TrustScore != null && x.TrustScore.OverallScore >= minScore.Value);
        }

        return await q.Take(50).ToListAsync(cancellationToken);
    }
}
