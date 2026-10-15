using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.BuyerOnboarding;
using VeriPass.Application.Suppliers;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using TrustScoreEntity = VeriPass.Domain.Entities.TrustScore;

namespace VeriPass.Application.UnitTests;

public class SupplierRiskPolicyTests
{
    [Theory]
    [InlineData(0, RiskLevel.HIGH)]
    [InlineData(10, RiskLevel.HIGH)]
    [InlineData(34.99, RiskLevel.HIGH)]
    [InlineData(35, RiskLevel.MEDIUM)]
    [InlineData(35.01, RiskLevel.MEDIUM)]
    [InlineData(50, RiskLevel.MEDIUM)]
    [InlineData(50.01, RiskLevel.MEDIUM)]
    [InlineData(69.99, RiskLevel.MEDIUM)]
    [InlineData(70, RiskLevel.MEDIUM)]
    [InlineData(70.01, RiskLevel.LOW)]
    [InlineData(75, RiskLevel.LOW)]
    [InlineData(90, RiskLevel.LOW)]
    [InlineData(99.99, RiskLevel.LOW)]
    [InlineData(100, RiskLevel.LOW)]
    public void DetermineRiskLevel_BoundaryValues_ShouldClassifyCorrectly(double score, RiskLevel expected)
    {
        var actual = SupplierRiskPolicy.DetermineRiskLevel(score);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(101)]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(double.NaN)]
    public void DetermineRiskLevel_InvalidValues_ShouldThrowArgumentOutOfRangeException(double invalidScore)
    {
        var act = () => SupplierRiskPolicy.DetermineRiskLevel(invalidScore);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DetermineRiskLevelSafe_NullOrInvalid_ShouldDefaultToHigh()
    {
        SupplierRiskPolicy.DetermineRiskLevelSafe(null).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.DetermineRiskLevelSafe(double.NaN).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.DetermineRiskLevelSafe(-5).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.DetermineRiskLevelSafe(105).Should().Be(RiskLevel.LOW);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(20, false)]
    [InlineData(34.99, false)]
    [InlineData(35, false)]
    [InlineData(49.99, false)]
    [InlineData(50, false)]      // 50 is NOT eligible
    [InlineData(50.01, true)]   // 50.01 is eligible
    [InlineData(51, true)]
    [InlineData(60, true)]
    [InlineData(70, true)]
    [InlineData(70.01, true)]
    [InlineData(90, true)]
    [InlineData(100, true)]
    public void IsEligibleForBuyer_BoundaryValues_ShouldValidateCorrectly(double score, bool expectedEligible)
    {
        var actual = SupplierRiskPolicy.IsEligibleForBuyer(score);
        actual.Should().Be(expectedEligible);
    }

    [Theory]
    [InlineData(-5, false)]
    [InlineData(105, false)]
    [InlineData(double.NaN, false)]
    public void IsEligibleForBuyer_InvalidValues_ShouldReturnFalse(double score, bool expected)
    {
        SupplierRiskPolicy.IsEligibleForBuyer(score).Should().Be(expected);
    }

    [Fact]
    public async Task SearchSuppliersQuery_WithBuyerOnly_ShouldFilterScoreAbove50AtDatabaseLevel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var org1 = new Organization { Id = Guid.NewGuid(), Name = "Org 1", Type = OrganizationType.SUPPLIER };
        var org2 = new Organization { Id = Guid.NewGuid(), Name = "Org 2", Type = OrganizationType.SUPPLIER };
        var org3 = new Organization { Id = Guid.NewGuid(), Name = "Org 3", Type = OrganizationType.SUPPLIER };
        var org4 = new Organization { Id = Guid.NewGuid(), Name = "Org 4", Type = OrganizationType.SUPPLIER };
        var org5 = new Organization { Id = Guid.NewGuid(), Name = "Org 5", Type = OrganizationType.SUPPLIER };
        context.Organizations.AddRange(org1, org2, org3, org4, org5);

        // Supplier 1: score = 30 (HIGH RISK, NOT eligible)
        var b1 = new Business { Id = Guid.NewGuid(), Organization = org1, OrganizationId = org1.Id, LegalName = "High Risk Supplier", BusinessIdNumber = "VP-01" };
        b1.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), Business = b1, BusinessId = b1.Id, OverallScore = 30 };

        // Supplier 2: score = 50 (MEDIUM RISK, NOT eligible because <= 50)
        var b2 = new Business { Id = Guid.NewGuid(), Organization = org2, OrganizationId = org2.Id, LegalName = "Medium Ineligible Supplier", BusinessIdNumber = "VP-02" };
        b2.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), Business = b2, BusinessId = b2.Id, OverallScore = 50 };

        // Supplier 3: score = 55 (MEDIUM RISK, ELIGIBLE because > 50)
        var b3 = new Business { Id = Guid.NewGuid(), Organization = org3, OrganizationId = org3.Id, LegalName = "Medium Eligible Supplier", BusinessIdNumber = "VP-03" };
        b3.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), Business = b3, BusinessId = b3.Id, OverallScore = 55 };

        // Supplier 4: score = 70 (MEDIUM RISK, ELIGIBLE)
        var b4 = new Business { Id = Guid.NewGuid(), Organization = org4, OrganizationId = org4.Id, LegalName = "Medium Boundary Supplier", BusinessIdNumber = "VP-04" };
        b4.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), Business = b4, BusinessId = b4.Id, OverallScore = 70 };

        // Supplier 5: score = 85 (LOW RISK, ELIGIBLE)
        var b5 = new Business { Id = Guid.NewGuid(), Organization = org5, OrganizationId = org5.Id, LegalName = "Low Risk Supplier", BusinessIdNumber = "VP-05" };
        b5.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), Business = b5, BusinessId = b5.Id, OverallScore = 85 };

        context.Businesses.AddRange(b1, b2, b3, b4, b5);
        context.TrustScores.AddRange(b1.TrustScore, b2.TrustScore, b3.TrustScore, b4.TrustScore, b5.TrustScore);
        await context.SaveChangesAsync();

        var handler = new SearchSuppliersQueryHandler(context);

        // Buyer search should ONLY return suppliers with score > 50 (b3, b4, b5)
        var buyerQuery = new SearchSuppliersQuery(BuyerOnly: true);
        var buyerResults = await handler.Handle(buyerQuery, CancellationToken.None);

        buyerResults.IsSuccess.Should().BeTrue();
        buyerResults.Value.Should().HaveCount(3);
        buyerResults.Value.Select(x => x.LegalName).Should().Contain(new[] { "Medium Eligible Supplier", "Medium Boundary Supplier", "Low Risk Supplier" });
        buyerResults.Value.Select(x => x.LegalName).Should().NotContain("High Risk Supplier");
        buyerResults.Value.Select(x => x.LegalName).Should().NotContain("Medium Ineligible Supplier");

        // Check authoritative risk levels on DTOs
        var lowSup = buyerResults.Value.First(x => x.TrustScore == 85);
        lowSup.RiskLevel.Should().Be("LOW");
        lowSup.BuyerEligible.Should().BeTrue();

        var medSup70 = buyerResults.Value.First(x => x.TrustScore == 70);
        medSup70.RiskLevel.Should().Be("MEDIUM");
        medSup70.BuyerEligible.Should().BeTrue();

        var medSup55 = buyerResults.Value.First(x => x.TrustScore == 55);
        medSup55.RiskLevel.Should().Be("MEDIUM");
        medSup55.BuyerEligible.Should().BeTrue();

        // Non-buyer search without BuyerOnly should return all suppliers
        var allQuery = new SearchSuppliersQuery(BuyerOnly: false);
        var allResults = await handler.Handle(allQuery, CancellationToken.None);
        allResults.Value.Should().HaveCount(5);

        var highSup = allResults.Value.First(x => x.TrustScore == 30);
        highSup.RiskLevel.Should().Be("HIGH");
        highSup.BuyerEligible.Should().BeFalse();

        var inelSup = allResults.Value.First(x => x.TrustScore == 50);
        inelSup.RiskLevel.Should().Be("MEDIUM");
        inelSup.BuyerEligible.Should().BeFalse();
    }

    [Fact]
    public async Task SearchSuppliersForOnboarding_ShouldFilterScoreAbove50AtDatabaseLevel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var org = new Organization { Id = Guid.NewGuid(), Name = "Supplier Org", Type = OrganizationType.SUPPLIER };
        context.Organizations.Add(org);

        var b1 = new Business { Id = Guid.NewGuid(), Organization = org, OrganizationId = org.Id, LegalName = "Ineligible 30", BusinessIdNumber = "VP-1" };
        b1.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), BusinessId = b1.Id, OverallScore = 30 };

        var b2 = new Business { Id = Guid.NewGuid(), Organization = org, OrganizationId = org.Id, LegalName = "Ineligible 50", BusinessIdNumber = "VP-2" };
        b2.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), BusinessId = b2.Id, OverallScore = 50 };

        var b3 = new Business { Id = Guid.NewGuid(), Organization = org, OrganizationId = org.Id, LegalName = "Eligible 75", BusinessIdNumber = "VP-3" };
        b3.TrustScore = new TrustScoreEntity { Id = Guid.NewGuid(), BusinessId = b3.Id, OverallScore = 75 };

        context.Businesses.AddRange(b1, b2, b3);
        context.TrustScores.AddRange(b1.TrustScore, b2.TrustScore, b3.TrustScore);
        await context.SaveChangesAsync();

        var handler = new SearchSuppliersForOnboardingQueryHandler(context);
        var res = await handler.Handle(new SearchSuppliersForOnboardingQuery(Query: null), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Should().HaveCount(1);
        res.Value.First().LegalName.Should().Be("Eligible 75");
        res.Value.First().TrustScore.Should().Be(75);
        res.Value.First().RiskLevel.Should().Be("LOW");
        res.Value.First().BuyerEligible.Should().BeTrue();
    }
}
