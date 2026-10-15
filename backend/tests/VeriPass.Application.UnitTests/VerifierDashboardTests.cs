using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Verification;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.Application.UnitTests;

public class VerifierDashboardTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetVerifierDashboard_KpiCalculations_ShouldAccuratelyCountStatuses()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Tata Electronics Ltd" };
        context.Businesses.Add(biz);

        // 3 Pending
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING }
        );

        // 2 In Review (1 assigned to current verifier, 1 to another)
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = currentVerifierId },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = Guid.NewGuid() }
        );

        // 4 Approved (2 via Status.VERIFIED, 2 via FinalDecision.APPROVED)
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.VERIFIED },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, FinalDecision = VerificationDecision.APPROVED },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.IN_PROGRESS, FinalDecision = VerificationDecision.APPROVED }
        );

        // 2 Rejected
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.REJECTED },
            new VerificationCase { BusinessId = biz.Id, FinalDecision = VerificationDecision.REJECTED }
        );

        // 2 Escalated
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.FLAGGED, FinalDecision = VerificationDecision.ESCALATED },
            new VerificationCase { BusinessId = biz.Id, FinalDecision = VerificationDecision.ESCALATED }
        );

        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value.Summary;

        summary.Pending.Should().Be(3);
        summary.InReview.Should().Be(2);
        summary.AssignedToCurrentVerifier.Should().Be(1);
        summary.Approved.Should().Be(4);
        summary.Rejected.Should().Be(2);
        summary.Escalated.Should().Be(2);
    }

    [Fact]
    public async Task GetVerifierDashboard_TodayCalculations_ShouldRespectDateBoundaries()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Reliance Industries" };
        context.Businesses.Add(biz);

        var today = DateTime.UtcNow;
        var yesterday = today.AddDays(-1);

        // Pending created today vs yesterday
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, CreatedAt = today },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, CreatedAt = today },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, CreatedAt = yesterday }
        );

        // Approved completed today vs yesterday
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = today },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = yesterday }
        );

        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Pending.Should().Be(3);
        result.Value.Summary.PendingTodayDelta.Should().Be(2);
        result.Value.Summary.Approved.Should().Be(2);
        result.Value.Summary.ApprovedTodayDelta.Should().Be(1);
    }

    [Fact]
    public async Task GetVerifierDashboard_PriorityOrdering_ShouldSortByEscalatedThenHighRiskThenAge()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Bharat Forge Ltd" };
        context.Businesses.Add(biz);

        var now = DateTime.UtcNow;

        var lowOld = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Status = VerificationStatus.PENDING,
            Priority = "LOW",
            RiskLevel = RiskLevel.LOW,
            CreatedAt = now.AddHours(-10)
        };

        var medRecent = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Status = VerificationStatus.PENDING,
            Priority = "MEDIUM",
            RiskLevel = RiskLevel.MEDIUM,
            CreatedAt = now.AddHours(-2)
        };

        var highOld = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Status = VerificationStatus.PENDING,
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            CreatedAt = now.AddHours(-4)
        };

        var highNew = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Status = VerificationStatus.PENDING,
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            CreatedAt = now.AddHours(-1)
        };

        var escalated = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            CreatedAt = now.AddHours(-3)
        };

        context.VerificationCases.AddRange(lowOld, medRecent, highOld, highNew, escalated);
        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var priorityList = result.Value.PriorityCases;

        priorityList.Should().HaveCount(5);
        // 1st: Escalated
        priorityList[0].Id.Should().Be(escalated.Id);
        // 2nd & 3rd: High risk / high priority, ordered by oldest first (highOld before highNew)
        priorityList[1].Id.Should().Be(highOld.Id);
        priorityList[2].Id.Should().Be(highNew.Id);
        // 4th: Medium
        priorityList[3].Id.Should().Be(medRecent.Id);
        // 5th: Low
        priorityList[4].Id.Should().Be(lowOld.Id);
    }

    [Fact]
    public async Task GetVerifierDashboard_Performance_ShouldAccuratelyCalculateRatesAndDuration()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Infosys BPM" };
        context.Businesses.Add(biz);

        var today = DateTime.UtcNow;

        // Case 1: completed today, duration 300s (5m)
        context.VerificationCases.Add(new VerificationCase
        {
            BusinessId = biz.Id,
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            StartedAt = today.AddMinutes(-5),
            CompletedAt = today
        });

        // Case 2: completed today, duration 600s (10m)
        context.VerificationCases.Add(new VerificationCase
        {
            BusinessId = biz.Id,
            Status = VerificationStatus.REJECTED,
            FinalDecision = VerificationDecision.REJECTED,
            StartedAt = today.AddMinutes(-10),
            CompletedAt = today
        });

        // Case 3: escalated
        context.VerificationCases.Add(new VerificationCase
        {
            BusinessId = biz.Id,
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED
        });

        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var perf = result.Value.Performance;

        perf.CasesReviewedToday.Should().Be(2);
        // Average of 300s and 600s = 450s (7m 30s)
        perf.AverageReviewTimeSeconds.Should().Be(450);
        // Approval rate: 1 approved out of 2 decided = 50.0%
        perf.ApprovalRate.Should().Be(50.0);
        // Escalation rate: 1 escalated out of (1 approved + 1 rejected + 1 escalated = 3) = 33.3%
        perf.EscalationRate.Should().Be(33.3);
    }

    [Fact]
    public async Task GetVerifierDashboard_RiskDistribution_ShouldCalculateExactPercentages()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Larsen & Toubro" };
        context.Businesses.Add(biz);

        // 7 Low, 2 Medium, 1 High (total 10 active cases)
        for (int i = 0; i < 7; i++)
            context.VerificationCases.Add(new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, RiskLevel = RiskLevel.LOW });

        for (int i = 0; i < 2; i++)
            context.VerificationCases.Add(new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, RiskLevel = RiskLevel.MEDIUM });

        context.VerificationCases.Add(new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, RiskLevel = RiskLevel.HIGH });

        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RiskDistribution.Low.Should().Be(70.0);
        result.Value.RiskDistribution.Medium.Should().Be(20.0);
        result.Value.RiskDistribution.High.Should().Be(10.0);
    }

    [Fact]
    public async Task GetVerifierDashboard_RecentDecisions_ShouldReturnLatestDecisions()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "Adani Ports" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "Wipro Ltd" };
        var verifier = new User { Id = Guid.NewGuid(), FirstName = "Vikram", LastName = "Iyer" };

        context.Businesses.AddRange(biz1, biz2);
        context.Users.Add(verifier);

        var now = DateTime.UtcNow;

        var dec1 = new VerificationCase
        {
            BusinessId = biz1.Id,
            VerificationType = "GST",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            CompletedAt = now.AddMinutes(-10),
            AssignedVerifierId = verifier.Id
        };

        var dec2 = new VerificationCase
        {
            BusinessId = biz2.Id,
            VerificationType = "PAN",
            Status = VerificationStatus.REJECTED,
            FinalDecision = VerificationDecision.REJECTED,
            CompletedAt = now.AddMinutes(-5),
            AssignedVerifierId = verifier.Id
        };

        context.VerificationCases.AddRange(dec1, dec2);
        await context.SaveChangesAsync();

        var handler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var decisions = result.Value.RecentDecisions;

        decisions.Should().HaveCount(2);
        // Newest first (dec2 before dec1)
        decisions[0].Id.Should().Be(dec2.Id);
        decisions[0].Decision.Should().Be("REJECTED");
        decisions[0].BusinessName.Should().Be("Wipro Ltd");
        decisions[0].VerifierName.Should().Be("Vikram Iyer");

        decisions[1].Id.Should().Be(dec1.Id);
        decisions[1].Decision.Should().Be("APPROVED");
        decisions[1].BusinessName.Should().Be("Adani Ports");
    }

    [Fact]
    public async Task Workflow_PendingToApproved_UpdatesDashboardAndCreatesAuditLog()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var mockTrustCalc = new Mock<ITrustScoreCalculator>();
        mockTrustCalc.Setup(t => t.CalculateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrustScoreCalculationResult
            {
                OverallScore = 85,
                IdentityScore = 80,
                CredentialScore = 85,
                ComplianceScore = 90,
                VerificationScore = 85,
                RiskScore = 85,
                Version = "1.0"
            });

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Sundaram Fasteners" };
        context.Businesses.Add(biz);

        var verificationCase = new VerificationCase
        {
            BusinessId = biz.Id,
            VerificationType = "GST",
            Status = VerificationStatus.PENDING,
            FinalDecision = VerificationDecision.NONE
        };
        context.VerificationCases.Add(verificationCase);
        await context.SaveChangesAsync();

        // Check initial dashboard
        var dashboardHandler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var initialDash = await dashboardHandler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);
        initialDash.Value.Summary.Pending.Should().Be(1);
        initialDash.Value.Summary.Approved.Should().Be(0);

        // Verifier approves case
        var approveHandler = new ApproveVerificationCommandHandler(context, mockTrustCalc.Object, mockCurrentUserService.Object);
        var approveResult = await approveHandler.Handle(new ApproveVerificationCommand(verificationCase.Id, "Verified against CBIC portal"), CancellationToken.None);
        approveResult.IsSuccess.Should().BeTrue();

        // Verify Database State
        var updatedCase = await context.VerificationCases.FindAsync(verificationCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.VERIFIED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.APPROVED);
        updatedCase.CompletedAt.Should().NotBeNull();

        // Verify Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "VERIFICATION_APPROVED");
        audit.Should().NotBeNull();
        audit!.ResourceId.Should().Be(verificationCase.Id.ToString());
        audit.TargetName.Should().Be("Sundaram Fasteners");

        // Verify Dashboard on refresh reflects the new database state
        var refreshedDash = await dashboardHandler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);
        refreshedDash.Value.Summary.Pending.Should().Be(0);
        refreshedDash.Value.Summary.Approved.Should().Be(1);
        refreshedDash.Value.Summary.ApprovedTodayDelta.Should().Be(1);
        refreshedDash.Value.Performance.CasesReviewedToday.Should().Be(1);
        refreshedDash.Value.RecentDecisions.Should().Contain(d => d.Id == verificationCase.Id && d.Decision == "APPROVED");
    }

    [Fact]
    public async Task Workflow_PendingToRejected_UpdatesDashboardAndCreatesAuditLog()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var mockTrustCalc = new Mock<ITrustScoreCalculator>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Apex Metal Castings" };
        context.Businesses.Add(biz);

        var verificationCase = new VerificationCase
        {
            BusinessId = biz.Id,
            VerificationType = "PAN",
            Status = VerificationStatus.PENDING,
            FinalDecision = VerificationDecision.NONE
        };
        context.VerificationCases.Add(verificationCase);
        await context.SaveChangesAsync();

        var rejectHandler = new RejectVerificationCommandHandler(context, mockTrustCalc.Object, mockCurrentUserService.Object);
        var rejectResult = await rejectHandler.Handle(new RejectVerificationCommand(verificationCase.Id, "PAN entity name mismatch"), CancellationToken.None);
        rejectResult.IsSuccess.Should().BeTrue();

        // Verify Database State
        var updatedCase = await context.VerificationCases.FindAsync(verificationCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.REJECTED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.REJECTED);
        updatedCase.CompletedAt.Should().NotBeNull();

        // Verify Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "VERIFICATION_REJECTED");
        audit.Should().NotBeNull();
        audit!.ResourceId.Should().Be(verificationCase.Id.ToString());

        // Verify Dashboard reflects rejection
        var dashboardHandler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var refreshedDash = await dashboardHandler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);
        refreshedDash.Value.Summary.Pending.Should().Be(0);
        refreshedDash.Value.Summary.Rejected.Should().Be(1);
        refreshedDash.Value.RecentDecisions.Should().Contain(d => d.Id == verificationCase.Id && d.Decision == "REJECTED");
    }

    [Fact]
    public async Task Workflow_PendingToEscalated_UpdatesDashboardAndCreatesAuditLog()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Zenith Dynamics" };
        context.Businesses.Add(biz);

        var verificationCase = new VerificationCase
        {
            BusinessId = biz.Id,
            VerificationType = "CIN",
            Status = VerificationStatus.PENDING,
            FinalDecision = VerificationDecision.NONE
        };
        context.VerificationCases.Add(verificationCase);
        await context.SaveChangesAsync();

        var escalateHandler = new EscalateVerificationCommandHandler(context, mockCurrentUserService.Object);
        var escalateResult = await escalateHandler.Handle(new EscalateVerificationCommand(verificationCase.Id, "Director DIN discrepancy requires senior review"), CancellationToken.None);
        escalateResult.IsSuccess.Should().BeTrue();

        // Verify Database State
        var updatedCase = await context.VerificationCases.FindAsync(verificationCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.FLAGGED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.ESCALATED);
        updatedCase.EscalatedAt.Should().NotBeNull();

        // Verify Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "VERIFICATION_ESCALATED");
        audit.Should().NotBeNull();
        audit!.ResourceId.Should().Be(verificationCase.Id.ToString());

        // Verify Dashboard reflects escalation
        var dashboardHandler = new GetVerifierDashboardQueryHandler(context, mockCurrentUserService.Object);
        var refreshedDash = await dashboardHandler.Handle(new GetVerifierDashboardQuery(), CancellationToken.None);
        refreshedDash.Value.Summary.Escalated.Should().Be(1);
    }
}
