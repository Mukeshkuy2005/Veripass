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

public class VerifierHistoryTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetVerifierHistory_ReturnsAuthoritativeRecordsWithUtcTimestamps()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var biz = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "ABC Manufacturing Pvt Ltd",
            Gstin = "09ABCDE1234F1Z5"
        };
        context.Businesses.Add(biz);

        var caseItem = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            VerificationType = "GST",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            DecisionReason = "Statutory GST registration reconciled against sovereign gateway.",
            Priority = "HIGH",
            RiskLevel = RiskLevel.LOW,
            CompletedAt = DateTime.UtcNow
        };
        context.VerificationCases.Add(caseItem);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Action = "VERIFICATION_APPROVED",
            Category = "VERIFICATION",
            ResourceType = "VERIFICATION_CASE",
            ResourceId = caseItem.Id.ToString(),
            TargetName = biz.LegalName,
            Result = "SUCCESS",
            EventCode = "AUD-18291001",
            CreatedAt = DateTime.UtcNow
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryQueryHandler(context, mockUserService.Object);
        var result = await handler.Handle(new GetVerifierHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().NotBeEmpty();
        var first = result.Value.Items.First();
        first.BusinessName.Should().Be("ABC Manufacturing Pvt Ltd");
        first.Decision.Should().Be("APPROVED");
        first.VerificationType.Should().Be("GST");
        first.DecisionAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        first.EvidenceExamined.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetVerifierHistory_FiltersByDecisionAndType()
    {
        using var context = CreateDbContext();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Acme Corp", Gstin = "29ABCDE1234F1Z5" };
        context.Businesses.Add(biz);

        // Case 1: Approved GST
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            VerificationType = "GST",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            CompletedAt = DateTime.UtcNow
        });

        // Case 2: Rejected PAN
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            VerificationType = "PAN",
            Status = VerificationStatus.REJECTED,
            FinalDecision = VerificationDecision.REJECTED,
            CompletedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryQueryHandler(context, mockUserService.Object);

        // Filter: Decision = REJECTED
        var resRejected = await handler.Handle(new GetVerifierHistoryQuery(Decision: "REJECTED"), CancellationToken.None);
        resRejected.IsSuccess.Should().BeTrue();
        resRejected.Value.Items.Should().OnlyContain(x => x.Decision == "REJECTED");

        // Filter: Type = GST
        var resGst = await handler.Handle(new GetVerifierHistoryQuery(VerificationType: "GST"), CancellationToken.None);
        resGst.IsSuccess.Should().BeTrue();
        resGst.Value.Items.Should().OnlyContain(x => x.VerificationType == "GST");
    }

    [Fact]
    public async Task GetVerifierHistory_FiltersBySearchQuery()
    {
        using var context = CreateDbContext();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "Reliance Logistics", Gstin = "27AABCR1234F1Z1" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "Bharat Steel Works", Gstin = "03BBBCS9876F1Z9" };
        context.Businesses.AddRange(biz1, biz2);

        context.VerificationCases.AddRange(
            new VerificationCase { Id = Guid.NewGuid(), BusinessId = biz1.Id, Business = biz1, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = DateTime.UtcNow },
            new VerificationCase { Id = Guid.NewGuid(), BusinessId = biz2.Id, Business = biz2, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryQueryHandler(context, mockUserService.Object);
        var result = await handler.Handle(new GetVerifierHistoryQuery(Search: "Bharat"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.First().BusinessName.Should().Be("Bharat Steel Works");
    }

    [Fact]
    public async Task GetVerifierHistory_EnforcesOrganizationIsolation()
    {
        using var context = CreateDbContext();
        var userOrgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.OrganizationId).Returns(userOrgId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" }); // non-admin

        var myBiz = new Business { Id = Guid.NewGuid(), OrganizationId = userOrgId, LegalName = "My Org Supplier" };
        var otherBiz = new Business { Id = Guid.NewGuid(), OrganizationId = otherOrgId, LegalName = "Other Org Supplier" };
        context.Businesses.AddRange(myBiz, otherBiz);

        context.VerificationCases.AddRange(
            new VerificationCase { Id = Guid.NewGuid(), BusinessId = myBiz.Id, Business = myBiz, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = DateTime.UtcNow },
            new VerificationCase { Id = Guid.NewGuid(), BusinessId = otherBiz.Id, Business = otherBiz, Status = VerificationStatus.VERIFIED, FinalDecision = VerificationDecision.APPROVED, CompletedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryQueryHandler(context, mockUserService.Object);
        var result = await handler.Handle(new GetVerifierHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().OnlyContain(x => x.BusinessName == "My Org Supplier");
        result.Value.Items.Should().NotContain(x => x.BusinessName == "Other Org Supplier");
    }

    [Fact]
    public async Task GetVerifierHistoryDetail_IdorPrevention_DeniesCrossOrgAccess()
    {
        using var context = CreateDbContext();
        var userOrgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();

        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.OrganizationId).Returns(userOrgId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var otherBiz = new Business { Id = Guid.NewGuid(), OrganizationId = otherOrgId, LegalName = "Foreign Business" };
        context.Businesses.Add(otherBiz);

        var caseItem = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = otherBiz.Id,
            Business = otherBiz,
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            CompletedAt = DateTime.UtcNow
        };
        context.VerificationCases.Add(caseItem);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventCode = "AUD-FOREIGN-99",
            ResourceId = caseItem.Id.ToString(),
            Category = "VERIFICATION",
            Action = "VERIFICATION_APPROVED"
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryEventDetailQueryHandler(context, mockUserService.Object);
        var result = await handler.Handle(new GetVerifierHistoryEventDetailQuery("AUD-FOREIGN-99"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetVerifierHistoryFilterOptions_ReturnsDynamicTypesAndVerifiers()
    {
        using var context = CreateDbContext();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        context.VerificationCases.AddRange(
            new VerificationCase { VerificationType = "GST" },
            new VerificationCase { VerificationType = "FSSAI" }
        );
        await context.SaveChangesAsync();

        var handler = new GetVerifierHistoryFilterOptionsQueryHandler(context, mockUserService.Object);
        var result = await handler.Handle(new GetVerifierHistoryFilterOptionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VerificationTypes.Should().Contain("FSSAI");
        result.Value.Decisions.Should().NotBeEmpty();
        result.Value.DateRanges.Should().NotBeEmpty();
    }
}
