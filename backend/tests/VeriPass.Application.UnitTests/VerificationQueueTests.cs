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

public class VerificationQueueTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetVerificationQueue_DefaultScope_ReturnsActionableCases()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(verifierId);

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "ABC Manufacturing Pvt Ltd" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "XYZ Heavy Industries" };
        context.Businesses.AddRange(biz1, biz2);

        // Actionable cases: PENDING, IN_PROGRESS, ESCALATED, REQUEST_MORE_INFO
        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz1.Id, VerificationType = "GST", Status = VerificationStatus.PENDING, Priority = "HIGH" },
            new VerificationCase { BusinessId = biz2.Id, VerificationType = "Address", Status = VerificationStatus.IN_PROGRESS, Priority = "NORMAL" },
            new VerificationCase { BusinessId = biz1.Id, VerificationType = "PAN", Status = VerificationStatus.FLAGGED, FinalDecision = VerificationDecision.ESCALATED },
            new VerificationCase { BusinessId = biz2.Id, VerificationType = "Bank", FinalDecision = VerificationDecision.REQUEST_MORE_INFO },
            // Non-actionable cases: APPROVED, REJECTED
            new VerificationCase { BusinessId = biz1.Id, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED },
            new VerificationCase { BusinessId = biz2.Id, Status = VerificationStatus.REJECTED, FinalDecision = VerificationDecision.REJECTED }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerificationQueueQuery(Scope: "all"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalCount.Should().Be(4);
        result.Value.Items.Should().HaveCount(4);
        result.Value.Summary.TotalWaiting.Should().Be(1);
    }

    [Fact]
    public async Task GetVerificationQueue_MyCasesScope_FiltersByCurrentVerifierClaim()
    {
        using var context = CreateDbContext();
        var currentVerifierId = Guid.NewGuid();
        var otherVerifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(currentVerifierId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Delta Dynamics" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, VerificationType = "GST", Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = currentVerifierId },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "PAN", Status = VerificationStatus.PENDING, AssignedVerifierId = currentVerifierId },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "Bank", Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = otherVerifierId },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "Address", Status = VerificationStatus.PENDING, AssignedVerifierId = null }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerificationQueueQuery(Scope: "my"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(c => c.AssignedTo != null && c.AssignedTo.Id == currentVerifierId);
        result.Value.Summary.MyCasesCount.Should().Be(2);
    }

    [Fact]
    public async Task GetVerificationQueue_Search_FiltersBySupplierNameCaseIdAndGstin()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "ABC Manufacturing", Gstin = "27AABCU9603R1ZM" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "Reliance Industries", Gstin = "24AABCR1234F1ZX" };
        context.Businesses.AddRange(biz1, biz2);

        var targetCaseId = Guid.NewGuid();
        context.VerificationCases.AddRange(
            new VerificationCase { Id = targetCaseId, BusinessId = biz1.Id, VerificationType = "GST", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz2.Id, VerificationType = "PAN", Status = VerificationStatus.PENDING }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        // Search by legal name
        var resultName = await handler.Handle(new GetVerificationQueueQuery(Search: "ABC Manufacturing"), CancellationToken.None);
        resultName.Value.Items.Should().HaveCount(1);
        resultName.Value.Items[0].BusinessName.Should().Be("ABC Manufacturing");

        // Search by GSTIN
        var resultGst = await handler.Handle(new GetVerificationQueueQuery(Search: "24AABCR"), CancellationToken.None);
        resultGst.Value.Items.Should().HaveCount(1);
        resultGst.Value.Items[0].BusinessName.Should().Be("Reliance Industries");

        // Search by Case ID
        var resultId = await handler.Handle(new GetVerificationQueueQuery(Search: targetCaseId.ToString()[..8]), CancellationToken.None);
        resultId.Value.Items.Should().HaveCount(1);
        resultId.Value.Items[0].Id.Should().Be(targetCaseId);
    }

    [Fact]
    public async Task GetVerificationQueue_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Tata Advanced Systems" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, VerificationType = "GST", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "PAN", Status = VerificationStatus.IN_PROGRESS },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "Bank", Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "ISO", Status = VerificationStatus.REJECTED, FinalDecision = VerificationDecision.REJECTED },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "MSME", Status = VerificationStatus.FLAGGED, FinalDecision = VerificationDecision.ESCALATED },
            new VerificationCase { BusinessId = biz.Id, VerificationType = "CIN", FinalDecision = VerificationDecision.REQUEST_MORE_INFO }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        var pendingRes = await handler.Handle(new GetVerificationQueueQuery(Status: "PENDING"), CancellationToken.None);
        pendingRes.Value.Items.Should().HaveCount(1);
        pendingRes.Value.Items[0].Status.Should().Be("PENDING");

        var inReviewRes = await handler.Handle(new GetVerificationQueueQuery(Status: "IN_REVIEW"), CancellationToken.None);
        inReviewRes.Value.Items.Should().HaveCount(1);
        inReviewRes.Value.Items[0].Status.Should().Be("IN_REVIEW");

        var approvedRes = await handler.Handle(new GetVerificationQueueQuery(Status: "APPROVED"), CancellationToken.None);
        approvedRes.Value.Items.Should().HaveCount(1);
        approvedRes.Value.Items[0].Status.Should().Be("APPROVED");

        var escalatedRes = await handler.Handle(new GetVerificationQueueQuery(Status: "ESCALATED"), CancellationToken.None);
        escalatedRes.Value.Items.Should().HaveCount(1);
        escalatedRes.Value.Items[0].Status.Should().Be("ESCALATED");

        var moreInfoRes = await handler.Handle(new GetVerificationQueueQuery(Status: "MORE_INFO_REQUIRED"), CancellationToken.None);
        moreInfoRes.Value.Items.Should().HaveCount(1);
        moreInfoRes.Value.Items[0].Status.Should().Be("MORE_INFO_REQUIRED");
    }

    [Fact]
    public async Task GetVerificationQueue_PriorityFilter_ReturnsOnlyMatchingPriority()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Bharat Electronics" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, Priority = "HIGH", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Priority = "URGENT", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Priority = "NORMAL", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Priority = "MEDIUM", Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Priority = "LOW", Status = VerificationStatus.PENDING }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        var highRes = await handler.Handle(new GetVerificationQueueQuery(Priority: "HIGH"), CancellationToken.None);
        highRes.Value.Items.Should().HaveCount(2);

        var medRes = await handler.Handle(new GetVerificationQueueQuery(Priority: "MEDIUM"), CancellationToken.None);
        medRes.Value.Items.Should().HaveCount(2);

        var lowRes = await handler.Handle(new GetVerificationQueueQuery(Priority: "LOW"), CancellationToken.None);
        lowRes.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetVerificationQueue_RiskFilter_ReturnsOnlyMatchingRisk()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Godrej Aerospace" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, RiskLevel = RiskLevel.HIGH, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, RiskLevel = RiskLevel.CRITICAL, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, RiskLevel = RiskLevel.MEDIUM, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, RiskLevel = RiskLevel.LOW, Status = VerificationStatus.PENDING }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        var highRes = await handler.Handle(new GetVerificationQueueQuery(Risk: "HIGH"), CancellationToken.None);
        highRes.Value.Items.Should().HaveCount(2);

        var medRes = await handler.Handle(new GetVerificationQueueQuery(Risk: "MEDIUM"), CancellationToken.None);
        medRes.Value.Items.Should().HaveCount(1);

        var lowRes = await handler.Handle(new GetVerificationQueueQuery(Risk: "LOW"), CancellationToken.None);
        lowRes.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetVerificationQueue_DateRange_ReturnsRecordsWithinUtcBoundary()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var now = DateTime.UtcNow;

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Kirloskar Oil Engines" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase { BusinessId = biz.Id, CreatedAt = now.AddHours(-2), Status = VerificationStatus.PENDING }, // 2h ago (today)
            new VerificationCase { BusinessId = biz.Id, CreatedAt = now.AddDays(-3), Status = VerificationStatus.PENDING },  // 3d ago (7d)
            new VerificationCase { BusinessId = biz.Id, CreatedAt = now.AddDays(-15), Status = VerificationStatus.PENDING }, // 15d ago (30d)
            new VerificationCase { BusinessId = biz.Id, CreatedAt = now.AddDays(-40), Status = VerificationStatus.PENDING }  // 40d ago (>30d)
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        var todayRes = await handler.Handle(new GetVerificationQueueQuery(DateRange: "24h"), CancellationToken.None);
        todayRes.Value.Items.Should().HaveCount(1);

        var sevenDayRes = await handler.Handle(new GetVerificationQueueQuery(DateRange: "7d"), CancellationToken.None);
        sevenDayRes.Value.Items.Should().HaveCount(2);

        var thirtyDayRes = await handler.Handle(new GetVerificationQueueQuery(DateRange: "30d"), CancellationToken.None);
        thirtyDayRes.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetVerificationQueue_Pagination_ReturnsCorrectPageAndPageSize()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Sundram Fasteners" };
        context.Businesses.Add(biz);

        for (int i = 0; i < 35; i++)
        {
            context.VerificationCases.Add(new VerificationCase
            {
                BusinessId = biz.Id,
                VerificationType = "GST",
                Status = VerificationStatus.PENDING,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);

        // Page 1 with pageSize 10
        var page1 = await handler.Handle(new GetVerificationQueueQuery(Page: 1, PageSize: 10), CancellationToken.None);
        page1.Value.TotalCount.Should().Be(35);
        page1.Value.TotalPages.Should().Be(4);
        page1.Value.Page.Should().Be(1);
        page1.Value.Items.Should().HaveCount(10);

        // Page 2 with pageSize 10
        var page2 = await handler.Handle(new GetVerificationQueueQuery(Page: 2, PageSize: 10), CancellationToken.None);
        page2.Value.Page.Should().Be(2);
        page2.Value.Items.Should().HaveCount(10);
        page2.Value.Items[0].Id.Should().NotBe(page1.Value.Items[0].Id);

        // Page 4 with pageSize 10 (last page with 5 items)
        var page4 = await handler.Handle(new GetVerificationQueueQuery(Page: 4, PageSize: 10), CancellationToken.None);
        page4.Value.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetVerificationQueue_SlaCalculation_ComputesAuthoritativeDeadlinesAndOverdue()
    {
        using var context = CreateDbContext();
        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var now = DateTime.UtcNow;

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Tata Steel" };
        context.Businesses.Add(biz);

        // Overdue High Priority case (submitted 6 hours ago with 4 hour SLA)
        var overdueCase = new VerificationCase
        {
            BusinessId = biz.Id,
            Priority = "HIGH",
            Status = VerificationStatus.PENDING,
            CreatedAt = now.AddHours(-6)
        };

        // Active Normal Priority case (submitted 2 hours ago with 24 hour SLA)
        var activeCase = new VerificationCase
        {
            BusinessId = biz.Id,
            Priority = "NORMAL",
            Status = VerificationStatus.PENDING,
            CreatedAt = now.AddHours(-2)
        };

        // Completed case (should show completed SLA)
        var completedCase = new VerificationCase
        {
            BusinessId = biz.Id,
            Priority = "HIGH",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            CreatedAt = now.AddHours(-8),
            CompletedAt = now.AddHours(-7)
        };

        context.VerificationCases.AddRange(overdueCase, activeCase, completedCase);
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);
        var result = await handler.Handle(new GetVerificationQueueQuery(Status: "ALL"), CancellationToken.None);

        var overdueDto = result.Value.Items.First(c => c.Id == overdueCase.Id);
        overdueDto.Sla.IsOverdue.Should().BeTrue();
        overdueDto.Sla.FormattedRemaining.Should().Be("Overdue");

        var activeDto = result.Value.Items.First(c => c.Id == activeCase.Id);
        activeDto.Sla.IsOverdue.Should().BeFalse();
        activeDto.Sla.FormattedRemaining.Should().Contain("remaining");

        var completedDto = result.Value.Items.First(c => c.Id == completedCase.Id);
        completedDto.Sla.FormattedRemaining.Should().Be("Completed");
    }

    [Fact]
    public async Task GetVerificationQueue_Summary_ReturnsAccurateCounters()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();

        var mockCurrentUserService = new Mock<ICurrentUserService>();
        mockCurrentUserService.Setup(s => s.UserId).Returns(verifierId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "L&T Heavy Engineering" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            // 3 Pending (1 assigned to current verifier)
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING, AssignedVerifierId = verifierId },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.PENDING },
            // 2 In Review (1 assigned to current verifier)
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = verifierId },
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.IN_PROGRESS, AssignedVerifierId = Guid.NewGuid() },
            // 1 Escalated
            new VerificationCase { BusinessId = biz.Id, Status = VerificationStatus.FLAGGED, FinalDecision = VerificationDecision.ESCALATED }
        );
        await context.SaveChangesAsync();

        var handler = new VerificationQueueHandler(context, mockCurrentUserService.Object);
        var summaryResult = await handler.Handle(new GetVerificationQueueSummaryQuery(), CancellationToken.None);

        summaryResult.IsSuccess.Should().BeTrue();
        summaryResult.Value.TotalWaiting.Should().Be(3);
        summaryResult.Value.MyCasesCount.Should().Be(2); // 1 PENDING + 1 IN_PROGRESS assigned to current verifier
        summaryResult.Value.InReviewCount.Should().Be(2);
        summaryResult.Value.ActionableCount.Should().Be(6);
    }
}
