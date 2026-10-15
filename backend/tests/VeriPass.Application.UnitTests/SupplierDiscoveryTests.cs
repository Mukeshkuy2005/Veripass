using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Suppliers;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using TrustScoreEntity = VeriPass.Domain.Entities.TrustScore;

namespace VeriPass.Application.UnitTests;

public class SupplierDiscoveryTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SearchSuppliers_ShouldFilterByIndustryStateAndTrustScore()
    {
        using var context = CreateDbContext();

        var org1 = new Organization { Id = Guid.NewGuid(), Name = "Kanpur Org", Type = OrganizationType.SUPPLIER };
        var org2 = new Organization { Id = Guid.NewGuid(), Name = "Bengaluru Org", Type = OrganizationType.SUPPLIER };
        context.Organizations.AddRange(org1, org2);

        var b1 = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org1.Id,
            Organization = org1,
            LegalName = "Kanpur Leather Works",
            BusinessType = "manufacturing",
            Description = "Footwear and industrial leather manufacturing",
            VerificationStatus = VerificationStatus.VERIFIED,
            BusinessIdNumber = "VP-IN-2026-001"
        };
        var addr1 = new BusinessAddress { Business = b1, BusinessId = b1.Id, State = "UttarPradesh", City = "Kanpur", IsPrimary = true };
        b1.Addresses.Add(addr1);
        b1.TrustScore = new TrustScoreEntity { Business = b1, BusinessId = b1.Id, OverallScore = 88 };

        var b2 = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2.Id,
            Organization = org2,
            LegalName = "Bengaluru Software Labs",
            BusinessType = "technology",
            Description = "SaaS software development",
            VerificationStatus = VerificationStatus.VERIFIED,
            BusinessIdNumber = "VP-IN-2026-002"
        };
        var addr2 = new BusinessAddress { Business = b2, BusinessId = b2.Id, State = "Karnataka", City = "Bengaluru", IsPrimary = true };
        b2.Addresses.Add(addr2);
        b2.TrustScore = new TrustScoreEntity { Business = b2, BusinessId = b2.Id, OverallScore = 75 };

        context.Businesses.AddRange(b1, b2);
        context.BusinessAddresses.AddRange(addr1, addr2);
        context.TrustScores.AddRange(b1.TrustScore, b2.TrustScore);
        await context.SaveChangesAsync();

        var handler = new SearchSuppliersQueryHandler(context);
        var query = new SearchSuppliersQuery(
            Industry: "manufacturing",
            State: "UttarPradesh",
            MinTrustScore: 80,
            VerificationStatus: "VERIFIED"
        );

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().LegalName.Should().Be("Kanpur Leather Works");
        result.Value.First().TrustScore.Should().Be(88);
    }

    [Fact]
    public async Task CompareSuppliers_ShouldReturnDetailedBreakdownScores()
    {
        using var context = CreateDbContext();

        var b1 = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Supplier Alpha",
            BusinessIdNumber = "VP-IN-2026-A1",
            Gstin = "27AAAAA0000A1Z5"
        };
        b1.TrustScore = new TrustScoreEntity
        {
            BusinessId = b1.Id,
            OverallScore = 90,
            IdentityScore = 95,
            CredentialScore = 90,
            ComplianceScore = 85,
            VerificationScore = 90,
            RiskScore = 95
        };

        var b2 = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Supplier Beta",
            BusinessIdNumber = "VP-IN-2026-B1",
            Gstin = "27BBBBB0000B1Z5"
        };
        b2.TrustScore = new TrustScoreEntity
        {
            BusinessId = b2.Id,
            OverallScore = 65,
            IdentityScore = 60,
            CredentialScore = 70,
            ComplianceScore = 70,
            VerificationScore = 50,
            RiskScore = 80
        };

        context.Businesses.AddRange(b1, b2);
        await context.SaveChangesAsync();

        var handler = new CompareSuppliersQueryHandler(context);
        var result = await handler.Handle(new CompareSuppliersQuery(new List<Guid> { b1.Id, b2.Id }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var alpha = result.Value.First(x => x.BusinessId == b1.Id);
        alpha.OverallTrustScore.Should().Be(90);
        alpha.IdentityScore.Should().Be(95);

        var beta = result.Value.First(x => x.BusinessId == b2.Id);
        beta.OverallTrustScore.Should().Be(65);
    }

    [Fact]
    public async Task ShortlistLifecycle_ShouldCreateAddAndRemoveSuppliers()
    {
        using var context = CreateDbContext();
        var buyerOrgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        // 1. Create Shortlist
        var createHandler = new CreateShortlistCommandHandler(context);
        var createRes = await createHandler.Handle(new CreateShortlistCommand(buyerOrgId, "Key Packaging Suppliers", "Suppliers for Q3 procurement"), CancellationToken.None);
        createRes.IsSuccess.Should().BeTrue();
        var shortlistId = createRes.Value;

        // 2. Add Supplier
        var addHandler = new AddToShortlistCommandHandler(context);
        var addRes = await addHandler.Handle(new AddToShortlistCommand(shortlistId, supplierId, "Shortlisted at vendor meet"), CancellationToken.None);
        addRes.IsSuccess.Should().BeTrue();

        var items = await context.SupplierShortlistItems.Where(i => i.ShortlistId == shortlistId).ToListAsync();
        items.Should().HaveCount(1);
        items.First().BusinessId.Should().Be(supplierId);

        // 3. Remove Supplier
        var removeHandler = new RemoveFromShortlistCommandHandler(context);
        var removeRes = await removeHandler.Handle(new RemoveFromShortlistCommand(shortlistId, supplierId), CancellationToken.None);
        removeRes.IsSuccess.Should().BeTrue();

        var remaining = await context.SupplierShortlistItems.Where(i => i.ShortlistId == shortlistId).ToListAsync();
        remaining.Should().BeEmpty();
    }
}
