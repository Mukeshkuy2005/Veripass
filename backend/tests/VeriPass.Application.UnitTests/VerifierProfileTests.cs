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

public class VerifierProfileTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetVerifierProfile_ReturnsAuthoritativeDataAndScope()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var user = new User
        {
            Id = verifierId,
            FirstName = "Rahul",
            LastName = "Sharma",
            Email = "verifier@veripass.in",
            Status = "ACTIVE",
            MfaEnabled = true,
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
            AssignedScopeJson = "{\"regions\":[\"Uttar Pradesh\",\"Delhi\",\"Haryana\"],\"categories\":[\"GST\",\"PAN\",\"Business Identity\"]}"
        };
        context.Users.Add(user);

        // Add 2 active refresh tokens
        context.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = verifierId,
                Token = "token1",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                IsRevoked = false,
                DeviceInfo = "Windows 11 / Chrome 128"
            },
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = verifierId,
                Token = "token2",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                IsRevoked = false,
                DeviceInfo = "iOS 18 / Safari"
            }
        );

        // Add 1 revoked token
        context.RefreshTokens.Add(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = verifierId,
                Token = "token3",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                IsRevoked = true,
                DeviceInfo = "Old Device"
            }
        );

        // Add verification case decisions
        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Acme Corp", Gstin = "09ABCDE1234F1Z5" };
        context.Businesses.Add(biz);

        context.VerificationCases.AddRange(
            new VerificationCase
            {
                Id = Guid.NewGuid(),
                BusinessId = biz.Id,
                Business = biz,
                AssignedVerifierId = verifierId,
                VerificationType = "GST",
                Status = VerificationStatus.VERIFIED,
                FinalDecision = VerificationDecision.APPROVED,
                StartedAt = DateTime.UtcNow.AddDays(-2),
                CompletedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(10)
            },
            new VerificationCase
            {
                Id = Guid.NewGuid(),
                BusinessId = biz.Id,
                Business = biz,
                AssignedVerifierId = verifierId,
                VerificationType = "PAN",
                Status = VerificationStatus.REJECTED,
                FinalDecision = VerificationDecision.REJECTED,
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CompletedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(6)
            }
        );

        await context.SaveChangesAsync();

        var handler = new VerifierProfileUseCasesHandler(context, mockUserService.Object);
        var res = await handler.Handle(new GetVerifierProfileQuery("30d"), CancellationToken.None);

        res.Succeeded.Should().BeTrue();
        var result = res.Data!;
        result.Profile.FullName.Should().Be("Rahul Sharma");
        result.Profile.Email.Should().Be("verifier@veripass.in");
        result.Profile.Role.Should().Be("VERIFIER");
        result.Profile.Status.Should().Be("ACTIVE");
        result.Profile.VerifierId.Should().StartWith("VER-");

        result.Scope.Regions.Should().Contain(new[] { "Uttar Pradesh", "Delhi", "Haryana" });
        result.Scope.VerificationCategories.Should().Contain(new[] { "GST", "PAN", "Business Identity" });

        result.Security.MfaEnabled.Should().BeTrue();
        result.Security.ActiveSessionCount.Should().Be(2);

        result.Activity.CasesReviewed.Should().Be(2);
        result.Activity.Approved.Should().Be(1);
        result.Activity.Rejected.Should().Be(1);
        result.Activity.Escalated.Should().Be(0);
        result.Activity.AverageReviewTimeSeconds.Should().Be(480); // (600s + 360s) / 2 = 480s (8 mins)
    }

    [Fact]
    public async Task GetVerifierActivity_AppliesDateRangeFilteringProperly()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "Acme Corp", Gstin = "09ABCDE1234F1Z5" };
        context.Businesses.Add(biz);

        // Case inside 7 days
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            AssignedVerifierId = verifierId,
            VerificationType = "GST",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED,
            StartedAt = DateTime.UtcNow.AddDays(-3),
            CompletedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(5)
        });

        // Case outside 7 days but within 30 days
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            AssignedVerifierId = verifierId,
            VerificationType = "PAN",
            Status = VerificationStatus.REJECTED,
            FinalDecision = VerificationDecision.REJECTED,
            StartedAt = DateTime.UtcNow.AddDays(-15),
            CompletedAt = DateTime.UtcNow.AddDays(-15).AddMinutes(15)
        });

        await context.SaveChangesAsync();

        var handler = new VerifierProfileUseCasesHandler(context, mockUserService.Object);

        // 7 days range
        var res7d = await handler.Handle(new GetVerifierActivityQuery("7d"), CancellationToken.None);
        res7d.Succeeded.Should().BeTrue();
        var act7d = res7d.Data!;
        act7d.CasesReviewed.Should().Be(1);
        act7d.Approved.Should().Be(1);
        act7d.Rejected.Should().Be(0);
        act7d.AverageReviewTimeSeconds.Should().Be(300);

        // 30 days range
        var res30d = await handler.Handle(new GetVerifierActivityQuery("30d"), CancellationToken.None);
        res30d.Succeeded.Should().BeTrue();
        var act30d = res30d.Data!;
        act30d.CasesReviewed.Should().Be(2);
        act30d.Approved.Should().Be(1);
        act30d.Rejected.Should().Be(1);
        act30d.AverageReviewTimeSeconds.Should().Be(600); // (300s + 900s)/2
    }

    [Fact]
    public async Task GetVerifierSessions_ReturnsOnlyActiveSessionsWithoutTokens()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var s1 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Token = "secret-token-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            IsRevoked = false,
            DeviceInfo = "Windows 11 / Chrome 128"
        };
        var s2 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Token = "secret-token-2",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            IsRevoked = false,
            DeviceInfo = "Mac OS / Firefox"
        };
        var sExpired = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Token = "secret-token-3",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // Expired
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            IsRevoked = false,
            DeviceInfo = "Linux / Chrome"
        };
        context.RefreshTokens.AddRange(s1, s2, sExpired);
        await context.SaveChangesAsync();

        var handler = new VerifierProfileUseCasesHandler(context, mockUserService.Object);
        var res = await handler.Handle(new GetVerifierSessionsQuery(), CancellationToken.None);

        res.Succeeded.Should().BeTrue();
        var result = res.Data!;
        result.Should().HaveCount(2);
        result.Should().NotContain(s => s.Id == sExpired.Id);
        result.First().IsCurrent.Should().BeTrue(); // s2 created more recently
    }

    [Fact]
    public async Task RevokeOtherSessions_RevokesSecondaryTokensAndAudits()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var s1 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Token = "secret-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            IsRevoked = false,
            DeviceInfo = "Windows 11 / Chrome 128"
        };
        var s2 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Token = "secret-2",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            IsRevoked = false,
            DeviceInfo = "iOS 18 / Safari"
        };
        context.RefreshTokens.AddRange(s1, s2);
        await context.SaveChangesAsync();

        var handler = new VerifierProfileUseCasesHandler(context, mockUserService.Object);
        var res = await handler.Handle(new RevokeVerifierOtherSessionsCommand(), CancellationToken.None);

        res.Succeeded.Should().BeTrue();
        res.Data.Should().Be(1);

        var updatedS1 = await context.RefreshTokens.FindAsync(s1.Id);
        var updatedS2 = await context.RefreshTokens.FindAsync(s2.Id);

        updatedS1!.IsRevoked.Should().BeTrue();
        updatedS2!.IsRevoked.Should().BeFalse(); // Current session remains active

        // Verify Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ALL_OTHER_SESSIONS_REVOKED");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(verifierId);
    }

    [Fact]
    public async Task ToggleMfa_UpdatesUserAndCreatesAuditRecord()
    {
        using var context = CreateDbContext();
        var verifierId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(verifierId);
        mockUserService.Setup(s => s.Roles).Returns(new List<string> { "VERIFIER" });

        var user = new User
        {
            Id = verifierId,
            FirstName = "Rahul",
            LastName = "Sharma",
            Email = "verifier@veripass.in",
            Status = "ACTIVE",
            MfaEnabled = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new VerifierProfileUseCasesHandler(context, mockUserService.Object);
        var res = await handler.Handle(new ToggleVerifierMfaCommand(true), CancellationToken.None);

        res.Succeeded.Should().BeTrue();
        res.Data.Should().BeTrue();

        var updatedUser = await context.Users.FindAsync(verifierId);
        updatedUser!.MfaEnabled.Should().BeTrue();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "MFA_ENABLED");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(verifierId);
    }
}
