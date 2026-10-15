using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MediatR;
using VeriPass.Application.Passport;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Infrastructure.Persistence;

namespace VeriPass.Application.UnitTests;

public class PassportUseCasesTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GeneratePassport_ShouldGenerateUniqueIdentifiers_AndActiveStatus()
    {
        using var context = CreateDbContext();
        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Zenith Solar Systems Ltd",
            BusinessIdNumber = "VP-IN-2026-5555"
        };
        context.Businesses.Add(business);
        await context.SaveChangesAsync();

        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetBusinessPassportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BusinessPassportDto>.Success(new BusinessPassportDto(
                Guid.NewGuid(), business.Id, business.LegalName, "VP-IN-2026-12345", "zenith-solar-systems-ltd-12345",
                "1.0", "ACTIVE", 90, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), DateTime.UtcNow,
                "/api/v1/public/passports/zenith-solar-systems-ltd-12345/qr", new(), new())));

        var handler = new GeneratePassportCommandHandler(context, mediatorMock.Object);
        var result = await handler.Handle(new GeneratePassportCommand(business.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var savedPassport = await context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == business.Id);
        savedPassport.Should().NotBeNull();
        savedPassport!.PassportNumber.Should().StartWith("VP-IN-");
        savedPassport.PublicIdentifier.Should().Contain("zenith-solar");
        savedPassport.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task PublishAndUnpublish_ShouldUpdatePassportPublicStatus()
    {
        using var context = CreateDbContext();
        var passport = new BusinessPassport
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            PassportNumber = "VP-IN-2026-7777",
            PublicIdentifier = "test-pub-7777",
            IsPublic = true,
            Status = "ACTIVE"
        };
        context.BusinessPassports.Add(passport);
        await context.SaveChangesAsync();

        var unpublishHandler = new UnpublishPassportCommandHandler(context);
        var unpublishResult = await unpublishHandler.Handle(new UnpublishPassportCommand(passport.BusinessId), CancellationToken.None);
        unpublishResult.IsSuccess.Should().BeTrue();

        var updated = await context.BusinessPassports.FindAsync(passport.Id);
        updated!.IsPublic.Should().BeFalse();
        updated.Status.Should().Be("UNPUBLISHED");

        var publishHandler = new PublishPassportCommandHandler(context);
        var publishResult = await publishHandler.Handle(new PublishPassportCommand(passport.BusinessId), CancellationToken.None);
        publishResult.IsSuccess.Should().BeTrue();

        var publishedAgain = await context.BusinessPassports.FindAsync(passport.Id);
        publishedAgain!.IsPublic.Should().BeTrue();
        publishedAgain.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task CreatePassportShare_ShouldGenerateShareTokenAndReturnUrl()
    {
        using var context = CreateDbContext();
        var passport = new BusinessPassport
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            PassportNumber = "VP-IN-2026-8888",
            PublicIdentifier = "acme-corp-8888",
            Status = "ACTIVE",
            IsPublic = true
        };
        context.BusinessPassports.Add(passport);
        await context.SaveChangesAsync();

        var handler = new CreatePassportShareCommandHandler(context);
        var command = new CreatePassportShareCommand(
            passport.BusinessId,
            "buyer@enterprise.com",
            "Enterprise Procurement",
            new Dictionary<string, bool> { ["pan"] = true, ["gst"] = true, ["bank"] = false },
            14,
            Guid.NewGuid()
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("/public/passport/acme-corp-8888?token=");

        var share = await context.PassportShares.FirstOrDefaultAsync(s => s.PassportId == passport.Id);
        share.Should().NotBeNull();
        share!.RecipientEmail.Should().Be("buyer@enterprise.com");
        share.AccessScopeJson.Should().Contain("\"pan\":true");
    }

    [Fact]
    public async Task VerifyPublicPassport_ShouldReturnAuthentic_WhenPassportIsActiveAndNotExpired()
    {
        using var context = CreateDbContext();
        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Bharat Electronics",
            BusinessIdNumber = "VP-IN-2026-1111"
        };
        var passport = new BusinessPassport
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Business = business,
            PassportNumber = "VP-IN-2026-1111",
            PublicIdentifier = "bharat-electronics-1111",
            Status = "ACTIVE",
            ExpiresAt = DateTime.UtcNow.AddMonths(6)
        };
        context.Businesses.Add(business);
        context.BusinessPassports.Add(passport);
        await context.SaveChangesAsync();

        var handler = new VerifyPublicPassportQueryHandler(context);
        var result = await handler.Handle(new VerifyPublicPassportQuery("bharat-electronics-1111"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsAuthentic.Should().BeTrue();
        result.Value.BusinessName.Should().Be("Bharat Electronics");
    }
}
