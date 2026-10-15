using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Auth;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;

namespace VeriPass.Application.UnitTests;

public class AuthCommandTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        // Seed Roles
        if (!context.Roles.Any())
        {
            context.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), Name = "SUPPLIER", Description = "Supplier Role" },
                new Role { Id = Guid.NewGuid(), Name = "BUYER", Description = "Buyer Role" },
                new Role { Id = Guid.NewGuid(), Name = "ADMIN", Description = "Admin Role" }
            );
            context.SaveChanges();
        }

        return context;
    }

    private (Mock<ITokenService> TokenMock, Mock<IPasswordHasher> HasherMock) CreateMocks()
    {
        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Organization?>(), It.IsAny<Role?>(), It.IsAny<IEnumerable<string>>()))
            .Returns("mock-access-token-jwt");
        tokenMock.Setup(t => t.GenerateRefreshToken())
            .Returns("mock-refresh-token-xyz");
        tokenMock.Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns<string>(s => "hash_" + s);

        var hasherMock = new Mock<IPasswordHasher>();
        hasherMock.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns<string>(p => "hashed_" + p);
        hasherMock.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((password, hash) => hash == "hashed_" + password);

        return (tokenMock, hasherMock);
    }

    [Fact]
    public async Task Register_ShouldCreateUserAndOrganization_AndReturnTokens()
    {
        using var context = CreateDbContext();
        var (tokenMock, hasherMock) = CreateMocks();

        var handler = new RegisterCommandHandler(context, tokenMock.Object, hasherMock.Object);
        var command = new RegisterCommand(
            "amit@precisionauto.in", "Password@123", "Amit", "Sharma",
            "Precision Auto Components", OrganizationType.SUPPLIER, "9876543210"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("mock-access-token-jwt");
        result.Value.RefreshToken.Should().Be("mock-refresh-token-xyz");
        result.Value.Email.Should().Be("amit@precisionauto.in");

        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "amit@precisionauto.in");
        savedUser.Should().NotBeNull();
        savedUser!.PasswordHash.Should().Be("hashed_Password@123");

        var savedOrg = await context.Organizations.FirstOrDefaultAsync(o => o.Name == "Precision Auto Components");
        savedOrg.Should().NotBeNull();
        savedOrg!.Type.Should().Be(OrganizationType.SUPPLIER);
    }

    [Fact]
    public async Task Register_ShouldFail_WhenEmailAlreadyExists()
    {
        using var context = CreateDbContext();
        var (tokenMock, hasherMock) = CreateMocks();

        context.Users.Add(new User
        {
            Email = "existing@supplier.in",
            PasswordHash = "hashed_Pass@123",
            FirstName = "Test",
            LastName = "User"
        });
        await context.SaveChangesAsync();

        var handler = new RegisterCommandHandler(context, tokenMock.Object, hasherMock.Object);
        var command = new RegisterCommand(
            "existing@supplier.in", "Password@123", "New", "User",
            "Another Org", OrganizationType.SUPPLIER, null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Login_ShouldSucceed_WithValidCredentials()
    {
        using var context = CreateDbContext();
        var (tokenMock, hasherMock) = CreateMocks();

        var role = await context.Roles.FirstAsync(r => r.Name == "SUPPLIER");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "valid@vendor.in",
            PasswordHash = "hashed_Secret@123",
            FirstName = "Rajesh",
            LastName = "Kumar",
            Status = "ACTIVE"
        };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Rajesh Tools", Type = OrganizationType.SUPPLIER };
        user.Memberships.Add(new OrganizationMember { OrganizationId = org.Id, RoleId = role.Id });

        context.Users.Add(user);
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        var handler = new LoginCommandHandler(context, tokenMock.Object, hasherMock.Object);
        var command = new LoginCommand("valid@vendor.in", "Secret@123");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("mock-access-token-jwt");
    }

    [Fact]
    public async Task Login_ShouldFail_WithInvalidPassword()
    {
        using var context = CreateDbContext();
        var (tokenMock, hasherMock) = CreateMocks();

        var user = new User
        {
            Email = "test@vendor.in",
            PasswordHash = "hashed_Correct@123",
            FirstName = "Rajesh",
            LastName = "Kumar",
            Status = "ACTIVE"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new LoginCommandHandler(context, tokenMock.Object, hasherMock.Object);
        var command = new LoginCommand("test@vendor.in", "WrongPassword@123");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid");
    }

    [Fact]
    public async Task VerifyEmail_ShouldMarkUserAsVerified()
    {
        using var context = CreateDbContext();
        var user = new User
        {
            Email = "unverified@vendor.in",
            FirstName = "Vijay",
            LastName = "Verma",
            EmailVerified = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new VerifyEmailCommandHandler(context);
        var result = await handler.Handle(new VerifyEmailCommand("unverified@vendor.in", "any-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Users.FirstAsync(u => u.Email == "unverified@vendor.in");
        updated.EmailVerified.Should().BeTrue();
    }
}
