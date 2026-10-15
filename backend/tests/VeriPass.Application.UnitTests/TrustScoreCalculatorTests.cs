using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using VeriPass.Infrastructure.TrustScoring;

namespace VeriPass.Application.UnitTests;

public class TrustScoreCalculatorTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReturnZero_WhenBusinessNotFound()
    {
        using var context = CreateDbContext();
        var calculator = new TrustScoreCalculator(context);

        var result = await calculator.CalculateAsync(Guid.NewGuid());

        result.OverallScore.Should().Be(0);
        result.SummaryReason.Should().Contain("not found");
    }

    [Fact]
    public async Task CalculateAsync_ShouldCalculateMaxIdentityScore_WhenAllStatutoryIdentifiersPresent()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Motors Corp", Email = "contact@tatamotors.com" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "Tata Motors Limited",
            Pan = "AAACT1234F",
            Gstin = "27AAACT1234F1Z5",
            Cin = "L28920MH1945PLC004520",
            UdyamNumber = "UDYAM-MH-01-0012345",
            BusinessIdNumber = "VP-IN-2026-1001"
        };
        business.Directors.Add(new Director { FullName = "N Chandrasekaran", Designation = "Director" });
        business.Addresses.Add(new BusinessAddress { Line1 = "Bombay House", City = "Mumbai", State = "Maharashtra", Pincode = "400001", IsPrimary = true });

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var result = await calculator.CalculateAsync(business.Id);

        // 25 (PAN) + 25 (GSTIN) + 20 (CIN/Udyam) + 15 (Director) + 15 (Primary Address) = 100
        result.IdentityScore.Should().Be(100);
        result.Version.Should().Be("v1.0");
    }

    [Fact]
    public async Task CalculateAsync_ShouldApplyCorrectWeightsSummingTo100Percent()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme Engineering", Email = "acme@vendor.in" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "Acme Engineering Pvt Ltd",
            Pan = "ABCDE1234F",
            Gstin = "27ABCDE1234F1Z5",
            BusinessIdNumber = "VP-IN-2026-1002"
        };
        context.Organizations.Add(org);
        context.Businesses.Add(business);

        var credType = new CredentialType { Id = Guid.NewGuid(), Code = "GST", Name = "GST Registration" };
        context.CredentialTypes.Add(credType);

        var cred = new Credential
        {
            BusinessId = business.Id,
            CredentialTypeId = credType.Id,
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        };
        business.Credentials.Add(cred);

        // Add medium compliance event
        business.ComplianceEvents.Add(new ComplianceEvent
        {
            BusinessId = business.Id,
            EventType = "GST_R1_DUE",
            Severity = ComplianceSeverity.MEDIUM,
            Title = "GSTR-1 Due Soon"
        });

        // Add low risk signal
        business.RiskSignals.Add(new RiskSignal
        {
            BusinessId = business.Id,
            SignalType = "ADDRESS_CHECK",
            Severity = RiskLevel.LOW,
            ScoreImpact = -5,
            Description = "Minor address string variance",
            Status = "ACTIVE"
        });

        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var result = await calculator.CalculateAsync(business.Id);

        // Verify bounds and valid weighting
        result.OverallScore.Should().BeInRange(0, 100);
        result.IdentityScore.Should().Be(50); // PAN (25) + GSTIN (25)
        result.CredentialScore.Should().Be(100); // 1 verified, 1 active out of 1 => 100%
        result.ComplianceScore.Should().Be(95); // 100 - 5 (MEDIUM) = 95
        result.VerificationScore.Should().Be(25); // 1 verified cred * 25 = 25
        result.RiskScore.Should().Be(95); // 100 - 5 = 95

        // Mathematical weighted expectation:
        // (50 * 0.25) + (100 * 0.25) + (95 * 0.20) + (25 * 0.20) + (95 * 0.10)
        // = 12.5 + 25 + 19 + 5 + 9.5 = 71.0 => 71
        result.OverallScore.Should().Be(71);
        result.Version.Should().Be("v1.0");
    }

    [Fact]
    public async Task CalculateAsync_ShouldClampScoreBetweenZeroAndOneHundred()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Risky Entity", Email = "risk@entity.in" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "Risky Logistics LLP",
            BusinessIdNumber = "VP-IN-2026-1003"
        };
        // Add multiple critical risks to exceed 100 deductions
        business.RiskSignals.Add(new RiskSignal { BusinessId = business.Id, ScoreImpact = -80, Status = "ACTIVE" });
        business.RiskSignals.Add(new RiskSignal { BusinessId = business.Id, ScoreImpact = -50, Status = "ACTIVE" });

        // Add critical compliance events
        business.ComplianceEvents.Add(new ComplianceEvent { BusinessId = business.Id, Severity = ComplianceSeverity.CRITICAL });
        business.ComplianceEvents.Add(new ComplianceEvent { BusinessId = business.Id, Severity = ComplianceSeverity.CRITICAL });
        business.ComplianceEvents.Add(new ComplianceEvent { BusinessId = business.Id, Severity = ComplianceSeverity.CRITICAL });
        business.ComplianceEvents.Add(new ComplianceEvent { BusinessId = business.Id, Severity = ComplianceSeverity.CRITICAL });

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var result = await calculator.CalculateAsync(business.Id);

        result.OverallScore.Should().Be(0);
        result.RiskScore.Should().Be(0);
        result.ComplianceScore.Should().Be(0);
    }
}
