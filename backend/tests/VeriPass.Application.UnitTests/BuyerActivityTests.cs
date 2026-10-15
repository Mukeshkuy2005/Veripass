using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Audit;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.Application.UnitTests;

public class BuyerActivityTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task TenantIsolation_QueryOnlyReturnsCurrentOrganizationLogs()
    {
        using var context = CreateDbContext();

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Mahindra Ltd", Type = OrganizationType.BUYER, Email = "buyer@mahindra.com" };
        var userA = new User { Id = Guid.NewGuid(), Email = "rahul@tatasteel.com", FirstName = "Rahul", LastName = "Sharma" };
        var userB = new User { Id = Guid.NewGuid(), Email = "anand@mahindra.com", FirstName = "Anand", LastName = "Mahindra" };

        context.Organizations.AddRange(orgA, orgB);
        context.Users.AddRange(userA, userB);

        // Org A events
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgA.Id,
            UserId = userA.Id,
            Action = "PASSPORT_VIEWED",
            Category = "PASSPORT",
            ResourceType = "BUSINESS_PASSPORT",
            ResourceId = "VP-IND-001",
            TargetName = "ABC Industrial Corp",
            Result = "SUCCESS",
            EventCode = "AUD-100001",
            CreatedAt = DateTime.UtcNow
        });

        // Org B events
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgB.Id,
            UserId = userB.Id,
            Action = "ONBOARDING_APPROVED",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = "ONB-999",
            TargetName = "Confidential Vendor",
            Result = "SUCCESS",
            EventCode = "AUD-200002",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var handler = new GetBuyerActivityQueryHandler(context);
        var query = new GetBuyerActivityQuery(OrganizationId: orgA.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].EventCode.Should().Be("AUD-100001");
        result.Value.Items[0].Target.Name.Should().Be("ABC Industrial Corp");
    }

    [Fact]
    public async Task ServerSideSearch_MatchesAcrossTargetUserAndAction()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var user1 = new User { Id = Guid.NewGuid(), Email = "rahul@tatasteel.com", FirstName = "Rahul", LastName = "Sharma" };
        var user2 = new User { Id = Guid.NewGuid(), Email = "priya@tatasteel.com", FirstName = "Priya", LastName = "Singh" };

        context.Organizations.Add(org);
        context.Users.AddRange(user1, user2);

        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                UserId = user1.Id,
                Action = "SUPPLIER_SHORTLISTED",
                Category = "SUPPLIER",
                ResourceType = "SUPPLIER",
                TargetName = "Delta Electric Drives",
                Result = "SUCCESS",
                EventCode = "AUD-DELTA-1",
                CreatedAt = DateTime.UtcNow
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                UserId = user2.Id,
                Action = "VERIFICATION_FAILED",
                Category = "VERIFICATION",
                ResourceType = "VERIFICATION",
                TargetName = "PQR Heavy Castings",
                Result = "FAILED",
                EventCode = "AUD-PQR-2",
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var handler = new GetBuyerActivityQueryHandler(context);

        // Search by target name
        var searchTarget = await handler.Handle(new GetBuyerActivityQuery(org.Id, Search: "Delta"), CancellationToken.None);
        searchTarget.Value.Items.Should().HaveCount(1);
        searchTarget.Value.Items[0].EventCode.Should().Be("AUD-DELTA-1");

        // Search by actor name
        var searchActor = await handler.Handle(new GetBuyerActivityQuery(org.Id, Search: "Priya"), CancellationToken.None);
        searchActor.Value.Items.Should().HaveCount(1);
        searchActor.Value.Items[0].EventCode.Should().Be("AUD-PQR-2");

        // Search by event code
        var searchCode = await handler.Handle(new GetBuyerActivityQuery(org.Id, Search: "AUD-DELTA"), CancellationToken.None);
        searchCode.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CategoryAndResultFilter_CorrectlyFiltersSubset()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        context.Organizations.Add(org);

        context.AuditLogs.AddRange(
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "ONBOARDING_APPROVED", Category = "ONBOARDING", ResourceType = "ONBOARDING", Result = "SUCCESS", CreatedAt = DateTime.UtcNow },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "ONBOARDING_REJECTED", Category = "ONBOARDING", ResourceType = "ONBOARDING", Result = "SUCCESS", CreatedAt = DateTime.UtcNow },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "VERIFICATION_FAILED", Category = "VERIFICATION", ResourceType = "VERIFICATION", Result = "FAILED", CreatedAt = DateTime.UtcNow },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "PASSPORT_ACCESS_REVOKED", Category = "SECURITY", ResourceType = "BUSINESS_PASSPORT", Result = "DENIED", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetBuyerActivityQueryHandler(context);

        // Filter by Category = ONBOARDING
        var catRes = await handler.Handle(new GetBuyerActivityQuery(org.Id, Category: "ONBOARDING"), CancellationToken.None);
        catRes.Value.TotalCount.Should().Be(2);

        // Filter by Result = DENIED
        var deniedRes = await handler.Handle(new GetBuyerActivityQuery(org.Id, Result: "DENIED"), CancellationToken.None);
        deniedRes.Value.TotalCount.Should().Be(1);
        deniedRes.Value.Items[0].Result.Should().Be("DENIED");
    }

    [Fact]
    public async Task SummaryQuery_CalculatesTodaySuccessFailedDeniedCorrectly()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        context.Organizations.Add(org);

        var now = DateTime.UtcNow;

        context.AuditLogs.AddRange(
            // Today events
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A1", Category = "GENERAL", ResourceType = "T", Result = "SUCCESS", CreatedAt = now },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A2", Category = "GENERAL", ResourceType = "T", Result = "SUCCESS", CreatedAt = now },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A3", Category = "GENERAL", ResourceType = "T", Result = "FAILED", CreatedAt = now },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A4", Category = "GENERAL", ResourceType = "T", Result = "DENIED", CreatedAt = now },

            // Past events (> 2 days ago)
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A5", Category = "GENERAL", ResourceType = "T", Result = "SUCCESS", CreatedAt = now.AddDays(-5) },
            new AuditLog { Id = Guid.NewGuid(), OrganizationId = org.Id, Action = "A6", Category = "GENERAL", ResourceType = "T", Result = "DENIED", CreatedAt = now.AddDays(-10) }
        );
        await context.SaveChangesAsync();

        var handler = new GetBuyerActivitySummaryQueryHandler(context);
        var summary = await handler.Handle(new GetBuyerActivitySummaryQuery(org.Id), CancellationToken.None);

        summary.IsSuccess.Should().BeTrue();
        summary.Value.Today.Should().Be(4);
        summary.Value.Success.Should().Be(3);
        summary.Value.Failed.Should().Be(1);
        summary.Value.Denied.Should().Be(2);
        summary.Value.Total.Should().Be(6);
    }

    [Fact]
    public async Task DetailQuery_RedactsSecurityContextForNonAdmins_ExposesForAdmins()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var user = new User { Id = Guid.NewGuid(), Email = "rahul@tatasteel.com", FirstName = "Rahul", LastName = "Sharma" };
        context.Organizations.Add(org);
        context.Users.Add(user);

        var logId = Guid.NewGuid();
        context.AuditLogs.Add(new AuditLog
        {
            Id = logId,
            OrganizationId = org.Id,
            UserId = user.Id,
            Action = "PASSPORT_VIEWED",
            Category = "PASSPORT",
            ResourceType = "BUSINESS_PASSPORT",
            ResourceId = "VP-IND-82341",
            TargetName = "ABC Electronics Pvt Ltd",
            Result = "SUCCESS",
            EventCode = "AUD-82F93A",
            IpAddress = "103.24.12.82",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0",
            CorrelationId = "REQ-82F93A-01",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new GetBuyerActivityDetailQueryHandler(context);

        // 1. Regular Procurement User (Non-Admin) -> Redacted
        var nonAdminResult = await handler.Handle(new GetBuyerActivityDetailQuery(logId, org.Id, IsAdmin: false), CancellationToken.None);
        nonAdminResult.IsSuccess.Should().BeTrue();
        nonAdminResult.Value.SecurityContext.IsRestricted.Should().BeTrue();
        nonAdminResult.Value.SecurityContext.IpAddress.Should().BeNull();
        nonAdminResult.Value.SecurityContext.Device.Should().BeNull();
        nonAdminResult.Value.SecurityContext.Session.Should().BeNull();
        nonAdminResult.Value.SecurityContext.RequestId.Should().Be("REDACTED");

        // 2. Privileged Administrator (Admin) -> Forensic details visible
        var adminResult = await handler.Handle(new GetBuyerActivityDetailQuery(logId, org.Id, IsAdmin: true), CancellationToken.None);
        adminResult.IsSuccess.Should().BeTrue();
        adminResult.Value.SecurityContext.IsRestricted.Should().BeFalse();
        adminResult.Value.SecurityContext.IpAddress.Should().Be("103.24.12.82");
        adminResult.Value.SecurityContext.Device.Should().Contain("Chrome/128.0");
        adminResult.Value.SecurityContext.RequestId.Should().Be("REQ-82F93A-01");
    }

    [Fact]
    public async Task DetailQuery_RejectsAccessToAnotherOrganizationsEvent()
    {
        using var context = CreateDbContext();
        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Mahindra Ltd", Type = OrganizationType.BUYER, Email = "buyer@mahindra.com" };

        var logId = Guid.NewGuid();
        context.AuditLogs.Add(new AuditLog
        {
            Id = logId,
            OrganizationId = orgB.Id,
            Action = "PASSPORT_VIEWED",
            ResourceType = "BUSINESS_PASSPORT",
            Result = "SUCCESS"
        });
        await context.SaveChangesAsync();

        var handler = new GetBuyerActivityDetailQueryHandler(context);
        var result = await handler.Handle(new GetBuyerActivityDetailQuery(logId, orgA.Id, IsAdmin: true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task ExportCommand_GeneratesCsvFile_AndSelfAudits()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var user = new User { Id = Guid.NewGuid(), Email = "rahul@tatasteel.com", FirstName = "Rahul", LastName = "Sharma" };
        context.Organizations.Add(org);
        context.Users.Add(user);

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = user.Id,
            Action = "ONBOARDING_APPROVED",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            TargetName = "XYZ Components Pvt Ltd",
            Result = "SUCCESS",
            EventCode = "AUD-100",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var auditServiceMock = new Mock<IAuditService>();
        auditServiceMock.Setup(s => s.RecordAsync(It.IsAny<RecordAuditOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());

        var handler = new ExportBuyerActivityCommandHandler(context, auditServiceMock.Object);
        var command = new ExportBuyerActivityCommand(org.Id, user.Id, Format: "CSV");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("text/csv");
        result.Value.Data.Should().NotBeEmpty();

        var csvText = System.Text.Encoding.UTF8.GetString(result.Value.Data);
        csvText.Should().Contain("Event ID,Timestamp (UTC),User Name");
        csvText.Should().Contain("XYZ Components Pvt Ltd");

        // Verify AUDIT_LOG_EXPORTED was recorded!
        auditServiceMock.Verify(s => s.RecordAsync(
            It.Is<RecordAuditOptions>(o => o.Action == "AUDIT_LOG_EXPORTED" && o.Category == "SECURITY"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
