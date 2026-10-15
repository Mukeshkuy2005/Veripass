using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.BackgroundJobs;
using VeriPass.Infrastructure.Persistence;

namespace VeriPass.Infrastructure.IntegrationTests;

public class RiskDetectionJobTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RiskDetectionJob_WhenPanGstinMismatch_ShouldCreateActiveRiskSignalAndRecalibrate()
    {
        using var context = CreateDbContext();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Inconsistent Traders Pvt Ltd",
            Gstin = "27AAACA1234F1Z5", // PAN embedded is AAACA1234F
            Pan = "ZZZZZ9999K",       // Mismatched PAN
            BusinessIdNumber = "VP-IN-2026-DISC",
            VerificationStatus = VerificationStatus.VERIFIED
        };
        context.Businesses.Add(business);

        var currentTrust = new Domain.Entities.TrustScore
        {
            BusinessId = business.Id,
            OverallScore = 80,
            RiskScore = 90
        };
        context.TrustScores.Add(currentTrust);
        await context.SaveChangesAsync();

        var trustCalculatorMock = new Mock<ITrustScoreCalculator>();
        trustCalculatorMock.Setup(t => t.CalculateAsync(business.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrustScoreCalculationResult
            {
                OverallScore = 55,
                RiskScore = 40,
                Version = "v1.0"
            });

        var job = new RiskDetectionJob(context, trustCalculatorMock.Object, NullLogger<RiskDetectionJob>.Instance);

        // Act
        await job.ExecuteAsync(CancellationToken.None);

        // Assert
        var signal = await context.RiskSignals.FirstOrDefaultAsync(r => r.BusinessId == business.Id && r.SignalType == "TAX_IDENTIFIER_MISMATCH");
        signal.Should().NotBeNull();
        signal!.Severity.Should().Be(RiskLevel.HIGH);
        signal.ScoreImpact.Should().Be(-25);

        // Verified trust score was updated and snapshot saved
        trustCalculatorMock.Verify(t => t.CalculateAsync(business.Id, It.IsAny<CancellationToken>()), Times.Once);

        var snapshot = await context.TrustScoreSnapshots.FirstOrDefaultAsync(s => s.BusinessId == business.Id && s.Reason.Contains("Risk Detection"));
        snapshot.Should().NotBeNull();
        snapshot!.OverallScore.Should().Be(55);
    }
}
