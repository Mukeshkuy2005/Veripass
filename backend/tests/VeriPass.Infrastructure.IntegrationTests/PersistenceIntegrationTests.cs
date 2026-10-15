using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;

namespace VeriPass.Infrastructure.IntegrationTests;

public class PersistenceIntegrationTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DbContext_ShouldPersistCompleteBusinessGraph_WithCredentialsAndTrustScores()
    {
        using var context = CreateDbContext();

        // 1. Create Organization
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Tata Steel Procurement Division",
            Type = OrganizationType.BUYER,
            Email = "procurement@tatasteel.com"
        };
        context.Organizations.Add(org);

        // 2. Create Business
        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Fasteners Pvt Ltd",
            TradeName = "Apex Fasteners",
            BusinessType = "manufacturing",
            Gstin = "27AAACA1234F1Z5",
            Pan = "AAACA1234F",
            BusinessIdNumber = "VP-IN-2026-9001",
            VerificationStatus = VerificationStatus.VERIFIED
        };
        business.Addresses.Add(new BusinessAddress
        {
            AddressType = "REGISTERED",
            Line1 = "Plot 42, MIDC Industrial Area",
            City = "Pune",
            State = "Maharashtra",
            Pincode = "411018",
            IsPrimary = true
        });
        business.Directors.Add(new Director
        {
            FullName = "Rajesh Sharma",
            Din = "01234567",
            Designation = "Managing Director"
        });
        business.BankAccounts.Add(new BankAccount
        {
            AccountHolderName = "Apex Fasteners Pvt Ltd",
            BankName = "State Bank of India",
            AccountNumberLast4 = "5566",
            Ifsc = "SBIN0001234",
            AccountType = "CURRENT",
            VerificationStatus = VerificationStatus.VERIFIED,
            IsPrimary = true
        });
        context.Businesses.Add(business);

        // 3. Credential Type & Credential
        var credType = new CredentialType
        {
            Id = Guid.NewGuid(),
            Code = "GSTIN",
            Name = "GST Registration Certificate",
            Category = "TAX",
            VerificationProvider = "GSTN",
            IsRequired = true
        };
        context.CredentialTypes.Add(credType);

        var credential = new Credential
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            CredentialTypeId = credType.Id,
            CredentialDisplayValue = "27AAACA1234F1Z5",
            Issuer = "GSTN",
            Status = CredentialStatus.VERIFIED,
            VerificationStatus = VerificationStatus.VERIFIED
        };
        context.Credentials.Add(credential);

        // 4. Verification Case
        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            CredentialId = credential.Id,
            VerificationType = "AUTOMATED_EXTRACTION",
            Status = VerificationStatus.VERIFIED,
            FinalDecision = VerificationDecision.APPROVED
        };
        vCase.Checks.Add(new VerificationCheck
        {
            CheckType = "REGISTRY_CONFIRMATION",
            Provider = "GSTN",
            Status = VerificationStatus.VERIFIED,
            ConfidenceScore = 99m
        });
        context.VerificationCases.Add(vCase);

        // 5. Trust Score & Snapshots (Section 60 model)
        var trustScore = new Domain.Entities.TrustScore
        {
            BusinessId = business.Id,
            OverallScore = 88,
            IdentityScore = 90,
            CredentialScore = 85,
            ComplianceScore = 85,
            VerificationScore = 90,
            RiskScore = 90
        };
        context.TrustScores.Add(trustScore);

        var snapshot = new TrustScoreSnapshot
        {
            BusinessId = business.Id,
            OverallScore = 88,
            IdentityScore = 90,
            CredentialScore = 85,
            ComplianceScore = 85,
            VerificationScore = 90,
            RiskScore = 90,
            CalculationVersion = "v1.0",
            Reason = "Initial Verification Completed"
        };
        context.TrustScoreSnapshots.Add(snapshot);

        // 6. Business Passport
        var passport = new BusinessPassport
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            PassportNumber = "VP-IN-2026-9001",
            PublicIdentifier = "apex-fasteners-pvt-ltd-9001",
            Version = "1.0",
            Status = "ACTIVE",
            VerificationScore = 88,
            QrTokenHash = "mock-qr-hash-12345"
        };
        context.BusinessPassports.Add(passport);

        // Save
        var savedCount = await context.SaveChangesAsync();
        savedCount.Should().BeGreaterThan(0);

        // Verify loaded graph
        var loadedBusiness = await context.Businesses
            .Include(b => b.Addresses)
            .Include(b => b.Directors)
            .Include(b => b.BankAccounts)
            .Include(b => b.Credentials)
            .FirstOrDefaultAsync(b => b.Id == business.Id);

        loadedBusiness.Should().NotBeNull();
        loadedBusiness!.Addresses.Should().HaveCount(1);
        loadedBusiness.Addresses.First().State.Should().Be("Maharashtra");
        loadedBusiness.Directors.Should().HaveCount(1);
        loadedBusiness.BankAccounts.Should().HaveCount(1);
        loadedBusiness.Credentials.Should().HaveCount(1);

        var loadedPassport = await context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == business.Id);
        loadedPassport.Should().NotBeNull();
        loadedPassport!.PassportNumber.Should().Be("VP-IN-2026-9001");

        var loadedSnapshot = await context.TrustScoreSnapshots.FirstOrDefaultAsync(s => s.BusinessId == business.Id);
        loadedSnapshot.Should().NotBeNull();
        loadedSnapshot!.CalculationVersion.Should().Be("v1.0");
    }
}
