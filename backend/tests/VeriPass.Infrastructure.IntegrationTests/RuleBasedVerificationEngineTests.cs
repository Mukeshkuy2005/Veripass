using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Domain.ValueObjects;
using VeriPass.Infrastructure.Persistence;
using VeriPass.Infrastructure.TrustScoring;
using Xunit;

namespace VeriPass.Infrastructure.IntegrationTests;

public class RuleBasedVerificationEngineTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RuleResolution_WhenBuyerProfileExists_UsesBuyerConfigurationAndMarksConfigurationSourceBuyer()
    {
        // Arrange
        using var context = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var supplier = new Business
        {
            Id = supplierId,
            LegalName = "Apex Industrial Goods Ltd",
            Gstin = "27AAACA1234F1Z5",
            Pan = "AAACA1234F",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        var gstType = new CredentialType { Id = Guid.NewGuid(), Code = "GST", Name = "GST", Category = "TAX" };
        var panType = new CredentialType { Id = Guid.NewGuid(), Code = "PAN", Name = "PAN", Category = "TAX" };
        context.CredentialTypes.AddRange(gstType, panType);

        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplierId,
            CredentialTypeId = gstType.Id,
            CredentialType = gstType,
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        supplier.Credentials.Add(new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = supplierId,
            CredentialTypeId = panType.Id,
            CredentialType = panType,
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });

        context.Businesses.Add(supplier);

        // Buyer custom profile with 60% GST and 40% PAN
        var profile = new BuyerVerificationProfile
        {
            BuyerOrganizationId = buyerId,
            Name = "Custom Buyer Profile",
            Version = 2,
            IsActive = true,
            IsDefault = true,
            TotalWeight = 100,
            Requirements = new List<CredentialRequirement>
            {
                new() { Type = "GST", IsRequired = true, Weight = 60, CredentialTypeId = gstType.Id },
                new() { Type = "PAN", IsRequired = true, Weight = 40, CredentialTypeId = panType.Id }
            }
        };
        context.BuyerVerificationProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // Act
        var result = await service.CalculateAndSaveScoreAsync(buyerId, supplierId);

        // Assert
        result.ConfigurationSource.Should().Be("BUYER");
        result.ProfileVersion.Should().Be(2);
        result.Score.Should().Be(100);
        result.Breakdown.Should().HaveCount(2);
        result.Breakdown.Single(b => b.CredentialType == "GST").Weight.Should().Be(60);
        result.Breakdown.Single(b => b.CredentialType == "PAN").Weight.Should().Be(40);

        var snapshot = await context.TrustScoreSnapshots.FirstAsync(s => s.BuyerId == buyerId && s.SupplierId == supplierId);
        snapshot.ConfigurationSource.Should().Be("BUYER");
    }

    [Fact]
    public async Task RuleResolution_WhenBuyerProfileDoesNotExist_FallsBackToPlatformAdminDefaults()
    {
        // Arrange
        using var context = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var supplier = new Business
        {
            Id = supplierId,
            LegalName = "Apex Industrial Goods Ltd",
            VerificationStatus = VerificationStatus.VERIFIED
        };
        context.Businesses.Add(supplier);

        // Platform Admin Default Rules seeded in compliance_rules
        context.ComplianceRules.AddRange(
            new ComplianceRule
            {
                Code = "RULE_GST_DEFAULT",
                Name = "GSTIN Checksum & Validity",
                Category = "STATUTORY",
                CredentialType = "GSTIN",
                ValidationType = "CHECKSUM",
                IsRequired = true,
                DefaultWeight = 30,
                IsActive = true
            },
            new ComplianceRule
            {
                Code = "RULE_PAN_DEFAULT",
                Name = "PAN Structure & Validation",
                Category = "STATUTORY",
                CredentialType = "PAN",
                ValidationType = "STRUCTURE",
                IsRequired = true,
                DefaultWeight = 15,
                IsActive = true
            },
            new ComplianceRule
            {
                Code = "RULE_UDYAM_DEFAULT",
                Name = "MSME Udyam Registration",
                Category = "STATUTORY",
                CredentialType = "UDYAM",
                ValidationType = "FORMAT",
                IsRequired = true,
                DefaultWeight = 15,
                IsActive = true
            },
            new ComplianceRule
            {
                Code = "RULE_BANK_DEFAULT",
                Name = "Bank Account & IFSC",
                Category = "FINANCIAL",
                CredentialType = "BANK",
                ValidationType = "DATA_MATCH",
                IsRequired = true,
                DefaultWeight = 25,
                IsActive = true
            },
            new ComplianceRule
            {
                Code = "RULE_CIN_DEFAULT",
                Name = "Corporate CIN",
                Category = "STATUTORY",
                CredentialType = "BUSINESS_REGISTRATION",
                ValidationType = "FORMAT",
                IsRequired = false,
                DefaultWeight = 15,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        var service = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // Act: Evaluate without any BuyerVerificationProfile in DB for buyerId
        var result = await service.CalculateAndSaveScoreAsync(buyerId, supplierId);

        // Assert
        result.ConfigurationSource.Should().Be("PLATFORM_DEFAULT");
        result.Breakdown.Should().HaveCount(5);
        result.Breakdown.Single(b => b.CredentialType == "GSTIN").Weight.Should().Be(30);
        result.Breakdown.Single(b => b.CredentialType == "PAN").Weight.Should().Be(15);
        result.Breakdown.Single(b => b.CredentialType == "UDYAM").Weight.Should().Be(15);
        result.Breakdown.Single(b => b.CredentialType == "BANK").Weight.Should().Be(25);
        result.Breakdown.Single(b => b.CredentialType == "BUSINESS_REGISTRATION").Weight.Should().Be(15);

        // Snapshot recorded in DB also reflects PLATFORM_DEFAULT
        var snapshot = await context.TrustScoreSnapshots.FirstAsync(s => s.BuyerId == buyerId && s.SupplierId == supplierId);
        snapshot.ConfigurationSource.Should().Be("PLATFORM_DEFAULT");
    }

    [Fact]
    public async Task Priority_BuyerRulesOverridePlatformAdminDefaults()
    {
        // Arrange
        using var context = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var supplier = new Business
        {
            Id = supplierId,
            LegalName = "Precision Tools Ltd",
            VerificationStatus = VerificationStatus.VERIFIED
        };
        context.Businesses.Add(supplier);

        // Platform Admin Default Rules: GSTIN (30), PAN (15), UDYAM (15), BANK (25), CIN (15)
        context.ComplianceRules.Add(new ComplianceRule
        {
            Code = "RULE_GST_DEFAULT",
            Name = "GSTIN Default",
            Category = "STATUTORY",
            CredentialType = "GSTIN",
            ValidationType = "CHECKSUM",
            IsRequired = true,
            DefaultWeight = 30,
            IsActive = true
        });

        // Buyer custom overrides GST weight to 70 and PAN to 30
        var profile = new BuyerVerificationProfile
        {
            BuyerOrganizationId = buyerId,
            Name = "High Priority Buyer Profile",
            Version = 1,
            IsActive = true,
            IsDefault = true,
            TotalWeight = 100,
            Requirements = new List<CredentialRequirement>
            {
                new() { Type = "GST", IsRequired = true, Weight = 70 },
                new() { Type = "PAN", IsRequired = false, Weight = 30 }
            }
        };
        context.BuyerVerificationProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // Act
        var result = await service.CalculateAndSaveScoreAsync(buyerId, supplierId);

        // Assert: Buyer profile completely overrides Admin defaults
        result.ConfigurationSource.Should().Be("BUYER");
        result.Breakdown.Should().HaveCount(2);
        result.Breakdown.Single(b => b.CredentialType == "GST").Weight.Should().Be(70);
        result.Breakdown.Single(b => b.CredentialType == "PAN").Weight.Should().Be(30);
    }

    [Fact]
    public void CredentialValidation_Gstin_ValidChecksum_PassesWithExplainableBreakdown()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        string base14 = "27AAACW1234F1Z";
        Gstin.VerifyChecksum(base14 + "0", out var expectedChar);
        string validGstin = base14 + expectedChar;

        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Wipro Industrial Tools Ltd",
            Gstin = validGstin,
            Pan = "AAACW1234F",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "GSTIN", isRequired: true, configuredRules: "CHECKSUM,NAME_MATCH");

        // Assert
        result.FinalStatus.Should().Be("APPROVED");
        result.RuleFailed.Should().BeFalse();
        result.RuleDetails.Should().NotBeEmpty();

        var checksumRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_GST_CHECKSUM");
        checksumRule.Should().NotBeNull();
        checksumRule!.Passed.Should().BeTrue();
        checksumRule.Status.Should().Be("PASSED");

        var panMatchRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_GST_PAN_MATCH");
        panMatchRule.Should().NotBeNull();
        panMatchRule!.Passed.Should().BeTrue();
    }

    [Fact]
    public void CredentialValidation_Gstin_CorruptedChecksum_DefinitivelyFailsAndReturnsRejected()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        string base14 = "27AAACW1234F1Z";
        Gstin.VerifyChecksum(base14 + "0", out var expectedChar);
        char corruptedChar = expectedChar == 'A' ? 'B' : 'A';
        string invalidChecksumGstin = base14 + corruptedChar;

        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Tools Ltd",
            Gstin = invalidChecksumGstin,
            Pan = "AAACW1234F"
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "GSTIN", isRequired: true, configuredRules: "CHECKSUM");

        // Assert
        result.FinalStatus.Should().Be("REJECTED");
        result.RuleFailed.Should().BeTrue();
        result.HasDefinitiveFailure.Should().BeTrue();

        var checksumRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_GST_CHECKSUM");
        checksumRule.Should().NotBeNull();
        checksumRule!.Passed.Should().BeFalse();
        checksumRule.Status.Should().Be("FAILED");
    }

    [Fact]
    public void CredentialValidation_Gstin_PanMismatch_FailsCrossDocumentMatch()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        string gstin = "27AAACW1234F1Z5"; // Embedded PAN: AAACW1234F

        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Tools Ltd",
            Gstin = gstin,
            Pan = "BBBPK9999D" // Mismatched corporate PAN
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "GSTIN", isRequired: true);

        // Assert
        result.FinalStatus.Should().Be("REJECTED");
        result.RuleFailed.Should().BeTrue();

        var panMatchRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_GST_PAN_MATCH");
        panMatchRule.Should().NotBeNull();
        panMatchRule!.Passed.Should().BeFalse();
        panMatchRule.Status.Should().Be("FAILED");
    }

    [Fact]
    public void CredentialValidation_Pan_Valid10CharacterFormat_Passes()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Wipro Limited",
            Pan = "AAACW1234F",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "PAN", isRequired: true, configuredRules: "NAME_MATCH");

        // Assert
        result.FinalStatus.Should().Be("APPROVED");
        result.RuleFailed.Should().BeFalse();

        var formatRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_PAN_FORMAT");
        formatRule.Should().NotBeNull();
        formatRule!.Passed.Should().BeTrue();

        var structureRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_PAN_STRUCTURE");
        structureRule.Should().NotBeNull();
        structureRule!.Passed.Should().BeTrue();
    }

    [Fact]
    public void CredentialValidation_Pan_AmbiguousNameMismatch_ReturnsEscalated()
    {
        // Arrange: 5th character 'W' does not match initial of legal name "Infosys Limited" ('I')
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Infosys Limited",
            Pan = "AAACW1234F"
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "PAN", isRequired: true, configuredRules: "NAME_MATCH");

        // Assert
        result.FinalStatus.Should().Be("ESCALATED");
        result.RequiresManualReview.Should().BeTrue();
        result.HasDefinitiveFailure.Should().BeFalse();

        var initialRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_PAN_INITIAL_MATCH");
        initialRule.Should().NotBeNull();
        initialRule!.Status.Should().Be("ESCALATED");
    }

    [Fact]
    public void CredentialValidation_Udyam_ValidFormat_Passes()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Micro Systems",
            UdyamNumber = "UDYAM-MH-01-0012345",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        // Act
        var result = engine.EvaluateCredential(supplier, "UDYAM", isRequired: true);

        // Assert
        result.FinalStatus.Should().Be("APPROVED");
        result.RuleFailed.Should().BeFalse();

        var udyamRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_UDYAM_FORMAT");
        udyamRule.Should().NotBeNull();
        udyamRule!.Passed.Should().BeTrue();
    }

    [Fact]
    public void CredentialValidation_Bank_BeneficiaryMatching_PassesWhenHighSimilarity()
    {
        // Arrange
        var engine = new RuleBasedVerificationEngine(NullLogger<RuleBasedVerificationEngine>.Instance);
        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Micro Systems Private Limited",
            VerificationStatus = VerificationStatus.VERIFIED
        };
        supplier.BankAccounts.Add(new BankAccount
        {
            BusinessId = supplier.Id,
            AccountNumberLast4 = "9012",
            Ifsc = "HDFC0000123",
            AccountHolderName = "Apex Micro Systems Pvt Ltd",
            IsPrimary = true,
            VerificationStatus = VerificationStatus.VERIFIED
        });

        // Act
        var result = engine.EvaluateCredential(supplier, "BANK", isRequired: true);

        // Assert
        result.FinalStatus.Should().Be("APPROVED");
        result.RuleFailed.Should().BeFalse();

        var ifscRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_BANK_IFSC_FORMAT");
        ifscRule.Should().NotBeNull();
        ifscRule!.Passed.Should().BeTrue();

        var matchRule = result.RuleDetails.FirstOrDefault(r => r.RuleCode == "RULE_BANK_BENEFICIARY_MATCH");
        matchRule.Should().NotBeNull();
        matchRule!.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task MandatoryCredentialGate_WhenRequiredCredentialIsRejected_MarksVerificationFailedEvenWithHighScore()
    {
        // Arrange: 4 credentials approved (weights 30+20+15+15 = 80), 1 required credential rejected (weight 20)
        using var context = CreateDbContext();
        var buyerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var supplier = new Business
        {
            Id = supplierId,
            LegalName = "Zenith Engineering Ltd",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        var gstType = new CredentialType { Id = Guid.NewGuid(), Code = "GST", Name = "GST", Category = "TAX" };
        var panType = new CredentialType { Id = Guid.NewGuid(), Code = "PAN", Name = "PAN", Category = "TAX" };
        var bankType = new CredentialType { Id = Guid.NewGuid(), Code = "BANK", Name = "BANK", Category = "FINANCIAL" };
        context.CredentialTypes.AddRange(gstType, panType, bankType);

        supplier.Credentials.Add(new Credential
        {
            BusinessId = supplierId,
            CredentialTypeId = gstType.Id,
            CredentialType = gstType,
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        supplier.Credentials.Add(new Credential
        {
            BusinessId = supplierId,
            CredentialTypeId = panType.Id,
            CredentialType = panType,
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        });
        // Mandatory Bank credential explicitly REJECTED
        supplier.Credentials.Add(new Credential
        {
            BusinessId = supplierId,
            CredentialTypeId = bankType.Id,
            CredentialType = bankType,
            Status = CredentialStatus.REJECTED,
            VerificationStatus = VerificationStatus.REJECTED
        });

        context.Businesses.Add(supplier);

        var profile = new BuyerVerificationProfile
        {
            BuyerOrganizationId = buyerId,
            Name = "Strict Compliance Profile",
            Version = 1,
            IsActive = true,
            IsDefault = true,
            TotalWeight = 100,
            Requirements = new List<CredentialRequirement>
            {
                new() { Type = "GST", IsRequired = true, Weight = 40, CredentialTypeId = gstType.Id },
                new() { Type = "PAN", IsRequired = true, Weight = 35, CredentialTypeId = panType.Id },
                new() { Type = "BANK", IsRequired = true, Weight = 25, CredentialTypeId = bankType.Id }
            }
        };
        context.BuyerVerificationProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new BuyerTrustScoreService(context, NullLogger<BuyerTrustScoreService>.Instance);

        // Act
        var result = await service.CalculateAndSaveScoreAsync(buyerId, supplierId);

        // Assert: Numerical score is 75 (High Trust), BUT ComplianceStatus is VerificationFailed and IsSelectable is false!
        result.Score.Should().Be(75);
        result.TrustLevel.Should().Be("High");
        result.ComplianceStatus.Should().Be("VerificationFailed");
        result.IsSelectable.Should().BeFalse();
    }
}
