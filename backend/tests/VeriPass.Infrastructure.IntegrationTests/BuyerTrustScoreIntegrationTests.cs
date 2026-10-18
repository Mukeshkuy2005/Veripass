using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeriPass.API.Controllers;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.TrustScore;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Domain.TrustScoring;
using VeriPass.Infrastructure.Persistence;
using VeriPass.Infrastructure.TrustScoring;

namespace VeriPass.Infrastructure.IntegrationTests;

public class BuyerTrustScoreIntegrationTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TrustScoreIntegration_" + Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private (Business supplier, Organization buyerA, Organization buyerB) SeedCommonTestData(ApplicationDbContext context)
    {
        // 1. Supplier Business with credentials
        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Acme Precision Components Ltd",
            TradeName = "Acme Components",
            BusinessType = "manufacturing",
            Gstin = "27AAACA1234F1Z5",
            Pan = "AAACA1234F",
            BusinessIdNumber = "VP-IN-2026-9002",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        // Credential Types
        var gstType = new CredentialType { Id = Guid.NewGuid(), Code = "GST", Name = "GST Registration", Category = "TAX", IsRequired = true };
        var panType = new CredentialType { Id = Guid.NewGuid(), Code = "PAN", Name = "PAN Card", Category = "TAX", IsRequired = true };
        var udyamType = new CredentialType { Id = Guid.NewGuid(), Code = "UDYAM", Name = "Udyam Certificate", Category = "MSME", IsRequired = true };
        var bankType = new CredentialType { Id = Guid.NewGuid(), Code = "BANK", Name = "Bank Account Verification", Category = "BANKING", IsRequired = true };
        var isoType = new CredentialType { Id = Guid.NewGuid(), Code = "ISO_9001", Name = "ISO 9001:2015", Category = "QUALITY", IsRequired = false };

        context.CredentialTypes.AddRange(gstType, panType, udyamType, bankType, isoType);

        // Credentials: GST, PAN, UDYAM, ISO are APPROVED; BANK is REJECTED
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            CredentialTypeId = gstType.Id,
            CredentialType = gstType,
            Issuer = "GSTN",
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            CredentialTypeId = panType.Id,
            CredentialType = panType,
            Issuer = "ITD",
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            CredentialTypeId = udyamType.Id,
            CredentialType = udyamType,
            Issuer = "MSME",
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            CredentialTypeId = bankType.Id,
            CredentialType = bankType,
            Issuer = "NPCI",
            Status = CredentialStatus.REJECTED,
            VerificationStatus = VerificationStatus.REJECTED
        });
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            CredentialTypeId = isoType.Id,
            CredentialType = isoType,
            Issuer = "TUV",
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });

        context.Businesses.Add(supplier);

        // 2. Buyer Organizations
        var buyerA = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Automotive OEM Corp (Buyer A)",
            Type = OrganizationType.BUYER,
            Email = "procurement@oemcorp.com"
        };
        var buyerB = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Heavy Engineering Corp (Buyer B)",
            Type = OrganizationType.BUYER,
            Email = "vendor@heavyeng.com"
        };

        context.Organizations.AddRange(buyerA, buyerB);

        // Buyer A Profile: weights 30/15/15/25/15 (Bank is 25) -> Sum = 100
        var profileAId = Guid.NewGuid();
        var profileA = new BuyerVerificationProfile
        {
            Id = profileAId,
            BuyerOrganizationId = buyerA.Id,
            Name = "Buyer A Standard Scoring Profile",
            Version = 1,
            IsActive = true,
            IsDefault = true,
            TotalWeight = 100,
            Requirements = new List<CredentialRequirement>
            {
                new() { ProfileId = profileAId, Type = "GST", IsRequired = true, Weight = 30, CredentialTypeId = gstType.Id },
                new() { ProfileId = profileAId, Type = "PAN", IsRequired = true, Weight = 15, CredentialTypeId = panType.Id },
                new() { ProfileId = profileAId, Type = "UDYAM", IsRequired = true, Weight = 15, CredentialTypeId = udyamType.Id },
                new() { ProfileId = profileAId, Type = "BANK", IsRequired = false, Weight = 25, CredentialTypeId = bankType.Id },
                new() { ProfileId = profileAId, Type = "ISO_9001", IsRequired = false, Weight = 15, CredentialTypeId = isoType.Id }
            }
        };

        // Buyer B Profile: same statuses, weights 20/10/10/40/20 (Bank is 40) -> Sum = 100
        var profileBId = Guid.NewGuid();
        var profileB = new BuyerVerificationProfile
        {
            Id = profileBId,
            BuyerOrganizationId = buyerB.Id,
            Name = "Buyer B Heavy Risk Scoring Profile",
            Version = 1,
            IsActive = true,
            IsDefault = true,
            TotalWeight = 100,
            Requirements = new List<CredentialRequirement>
            {
                new() { ProfileId = profileBId, Type = "GST", IsRequired = true, Weight = 20, CredentialTypeId = gstType.Id },
                new() { ProfileId = profileBId, Type = "PAN", IsRequired = true, Weight = 10, CredentialTypeId = panType.Id },
                new() { ProfileId = profileBId, Type = "UDYAM", IsRequired = true, Weight = 10, CredentialTypeId = udyamType.Id },
                new() { ProfileId = profileBId, Type = "BANK", IsRequired = false, Weight = 40, CredentialTypeId = bankType.Id },
                new() { ProfileId = profileBId, Type = "ISO_9001", IsRequired = false, Weight = 20, CredentialTypeId = isoType.Id }
            }
        };

        context.BuyerVerificationProfiles.AddRange(profileA, profileB);
        context.SaveChanges();

        return (supplier, buyerA, buyerB);
    }

    [Fact]
    public async Task SameSupplierUnderTwoBuyers_YieldsTwoDifferentSnapshots()
    {
        // Arrange
        using var context = CreateDbContext();
        var (supplier, buyerA, buyerB) = SeedCommonTestData(context);
        var scoreService = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // Act: Calculate and snapshot for Buyer A
        var resultA = await scoreService.CalculateAndSaveScoreAsync(buyerA.Id, supplier.Id);

        // Act: Calculate and snapshot for Buyer B
        var resultB = await scoreService.CalculateAndSaveScoreAsync(buyerB.Id, supplier.Id);

        // Assert: Buyer A yields Score 75, High Trust (30+15+15+0+15 = 75)
        resultA.Score.Should().Be(75);
        resultA.TrustLevel.Should().Be("High");
        resultA.BuyerId.Should().Be(buyerA.Id);

        // Assert: Buyer B yields Score 60, Medium Trust (20+10+10+0+20 = 60)
        resultB.Score.Should().Be(60);
        resultB.TrustLevel.Should().Be("Medium");
        resultB.BuyerId.Should().Be(buyerB.Id);

        // Assert: Database persistence has 2 distinct snapshots for the same supplier
        var snapshots = await context.TrustScoreSnapshots
            .Where(s => s.SupplierId == supplier.Id)
            .ToListAsync();

        snapshots.Should().HaveCount(2);

        var snapshotA = snapshots.Single(s => s.BuyerId == buyerA.Id);
        snapshotA.Score.Should().Be(75);
        snapshotA.Level.Should().Be("High");

        var snapshotB = snapshots.Single(s => s.BuyerId == buyerB.Id);
        snapshotB.Score.Should().Be(60);
        snapshotB.Level.Should().Be("Medium");
    }

    [Fact]
    public async Task EditingBuyerWeights_CreatesNewProfileVersion_AndDoesNotMutateOldSnapshots()
    {
        // Arrange
        using var context = CreateDbContext();
        var (supplier, buyerA, _) = SeedCommonTestData(context);
        var scoreService = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // 1. First calculation for Buyer A at ProfileVersion = 1
        var initialResult = await scoreService.CalculateAndSaveScoreAsync(buyerA.Id, supplier.Id);
        initialResult.ProfileVersion.Should().Be(1);
        initialResult.Score.Should().Be(75);

        var firstSnapshot = await context.TrustScoreSnapshots
            .AsNoTracking()
            .SingleAsync(s => s.BuyerId == buyerA.Id && s.SupplierId == supplier.Id);

        var originalSnapshotId = firstSnapshot.Id;
        var originalCalculatedAt = firstSnapshot.CalculatedAt;

        // 2. Edit Buyer A profile weights: new balanced weights 20/20/20/20/20 (Sum = 100)
        var handler = new UpdateBuyerVerificationProfileCommandHandler(context, scoreService);
        var updateCommand = new UpdateBuyerVerificationProfileCommand(
            BuyerOrganizationId: buyerA.Id,
            Name: "Buyer A Updated Profile v2",
            Description: "Equal 20% weights",
            Requirements: new List<RequirementConfigDto>
            {
                new("GST", true, 20, null),
                new("PAN", true, 20, null),
                new("UDYAM", true, 20, null),
                new("BANK", false, 20, null),
                new("ISO_9001", false, 20, null)
            }
        );

        var updateResult = await handler.Handle(updateCommand, CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Version.Should().Be(2);

        // Verify profile version in DB is now 2
        var updatedProfile = await context.BuyerVerificationProfiles
            .SingleAsync(p => p.BuyerOrganizationId == buyerA.Id);
        updatedProfile.Version.Should().Be(2);

        // 3. The handler automatically recalculated score under ProfileVersion = 2
        // Since Bank is rejected (0) and the other 4 are approved (20*4 = 80)
        var secondResult = await scoreService.GetLatestScoreAsync(buyerA.Id, supplier.Id);
        secondResult.ProfileVersion.Should().Be(2);
        secondResult.Score.Should().Be(80);

        // 4. Assert: Old snapshot is preserved and NOT mutated!
        var allSnapshots = await context.TrustScoreSnapshots
            .Where(s => s.BuyerId == buyerA.Id && s.SupplierId == supplier.Id)
            .OrderBy(s => s.ProfileVersion)
            .ToListAsync();

        allSnapshots.Should().HaveCount(2);

        var preservedSnapshot = allSnapshots[0];
        preservedSnapshot.Id.Should().Be(originalSnapshotId);
        preservedSnapshot.ProfileVersion.Should().Be(1);
        preservedSnapshot.Score.Should().Be(75);
        preservedSnapshot.Level.Should().Be("High");
        preservedSnapshot.CalculatedAt.Should().Be(originalCalculatedAt);

        var newSnapshot = allSnapshots[1];
        newSnapshot.Id.Should().NotBe(originalSnapshotId);
        newSnapshot.ProfileVersion.Should().Be(2);
        newSnapshot.Score.Should().Be(80);
        newSnapshot.Level.Should().Be("High");
    }

    [Fact]
    public async Task ProfileWithWeightsSummingTo99_IsRejectedWith400()
    {
        // 1. Controller test: Calling BuyerTrustScoresController.UpdateScoringProfile with sum = 99
        var mockMediator = new Mock<IMediator>();
        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var buyerId = Guid.NewGuid();
        mockCurrentUserService.Setup(s => s.OrganizationId).Returns(buyerId);

        var controller = new BuyerTrustScoresController(mockMediator.Object, mockCurrentUserService.Object);

        var invalidRequest = new UpdateProfileRequest
        {
            Name = "Invalid Profile (Sum 99)",
            Requirements = new List<RequirementConfigRequest>
            {
                new() { Type = "GST", IsRequired = true, Weight = 50 },
                new() { Type = "PAN", IsRequired = true, Weight = 49 } // Sum = 99
            }
        };

        var actionResult = await controller.UpdateScoringProfile(buyerId.ToString(), invalidRequest);

        // Assert HTTP 400 Bad Request
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)actionResult;
        badRequestResult.StatusCode.Should().Be(400);

        // Check error message explicitly flags the invalid sum
        var messageProp = badRequestResult.Value?.GetType().GetProperty("message");
        var errorMessage = messageProp?.GetValue(badRequestResult.Value) as string;
        errorMessage.Should().Contain("Buyer profile weights must sum to exactly 100. Current sum is 99.");

        // 2. Application Command Handler validation test
        using var context = CreateDbContext();
        var mockTrustScoreService = new Mock<IBuyerTrustScoreService>();
        var handler = new UpdateBuyerVerificationProfileCommandHandler(context, mockTrustScoreService.Object);

        var command = new UpdateBuyerVerificationProfileCommand(
            BuyerOrganizationId: buyerId,
            Name: "Invalid 99 Profile",
            Description: null,
            Requirements: new List<RequirementConfigDto>
            {
                new("GST", true, 50, null),
                new("PAN", true, 49, null)
            }
        );

        var commandResult = await handler.Handle(command, CancellationToken.None);
        commandResult.IsSuccess.Should().BeFalse();
        commandResult.Error.Should().Contain("Buyer profile weights must sum to exactly 100. Current sum is 99.");

        // 3. Domain Entity Validation test
        var domainProfile = new BuyerVerificationProfile
        {
            BuyerOrganizationId = buyerId,
            Name = "Domain Invalid Profile",
            Version = 1,
            TotalWeight = 99,
            Requirements = new List<CredentialRequirement>
            {
                new() { Type = "GST", Weight = 50, IsRequired = true },
                new() { Type = "PAN", Weight = 49, IsRequired = true }
            }
        };

        var validateAction = () => domainProfile.ValidateWeights();
        validateAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*must sum to exactly 100*");
    }
}
