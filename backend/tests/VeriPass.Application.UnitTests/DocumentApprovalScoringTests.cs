using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Verification;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using VeriPass.Infrastructure.TrustScoring;
using TrustScoreEntity = VeriPass.Domain.Entities.TrustScore;

namespace VeriPass.Application.UnitTests;

public class DocumentApprovalScoringTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DocumentApproval_ShouldIncreaseScore_AndCrossBuyerEligibilityThreshold()
    {
        using var context = CreateDbContext();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var org = new Organization { Id = Guid.NewGuid(), Name = "ABC Industrial Corp", Email = "ops@abc.com" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "ABC Industrial Dynamics",
            BusinessIdNumber = "VP-IN-2026-92644",
            VerificationStatus = VerificationStatus.PENDING
        };

        var initialTrustScore = new TrustScoreEntity
        {
            BusinessId = business.Id,
            OverallScore = 30,
            IdentityScore = 0,
            CredentialScore = 0,
            ComplianceScore = 100,
            VerificationScore = 0,
            RiskScore = 100
        };

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            DocumentType = "GST_CERTIFICATE",
            OriginalFileName = "ABC_GST_CERT.pdf",
            Status = DocumentStatus.PROCESSED
        };

        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            VerificationType = "GST",
            Status = VerificationStatus.PENDING,
            FinalDecision = VerificationDecision.NONE
        };

        var evidence = new VerificationEvidence
        {
            Id = Guid.NewGuid(),
            VerificationCaseId = vCase.Id,
            DocumentId = doc.Id,
            EvidenceType = "PRIMARY_CREDENTIAL",
            FieldName = "Gstin",
            ActualValue = "27AAACB1234F1Z5",
            MatchStatus = "MATCH"
        };
        vCase.Evidences.Add(evidence);

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        context.TrustScores.Add(initialTrustScore);
        context.Documents.Add(doc);
        context.VerificationCases.Add(vCase);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var handler = new ApproveVerificationCommandHandler(context, calculator, currentUserServiceMock.Object);

        // Before approval: Score = 30, Risk = HIGH, Buyer Eligible = NO
        SupplierRiskPolicy.DetermineRiskLevel(30).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.IsEligibleForBuyer(30).Should().BeFalse();

        // Act: Verifier approves document / case
        var result = await handler.Handle(new ApproveVerificationCommand(vCase.Id, "GST portal validated"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be("APPROVED");
        result.Value.Supplier.RiskScore.Should().BeGreaterThan(50);
        result.Value.Supplier.RiskLevel.Should().Be("MEDIUM");
        result.Value.Supplier.BuyerEligible.Should().BeTrue();

        // Database state verified
        var updatedDoc = await context.Documents.FindAsync(doc.Id);
        updatedDoc!.Status.Should().Be(DocumentStatus.APPROVED);

        var updatedCase = await context.VerificationCases.FindAsync(vCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.VERIFIED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.APPROVED);

        var updatedScore = await context.TrustScores.FirstOrDefaultAsync(t => t.BusinessId == business.Id);
        updatedScore!.OverallScore.Should().Be(result.Value.Supplier.RiskScore);
        updatedScore.OverallScore.Should().BeGreaterThan(50);
        updatedScore.VerificationScore.Should().Be(25); // 1 verified statutory factor

        // Audit log verified
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "DOCUMENT_APPROVED");
        audit.Should().NotBeNull();
        audit!.ResourceId.Should().Be(doc.Id.ToString());
    }

    [Fact]
    public async Task DuplicateApproval_MustBePrevented_AndNotAddScoreTwice()
    {
        using var context = CreateDbContext();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(Guid.NewGuid());

        var org = new Organization { Id = Guid.NewGuid(), Name = "ABC Industrial Corp", Email = "ops@abc.com" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "ABC Industrial Dynamics",
            BusinessIdNumber = "VP-IN-2026-92644"
        };
        var trustScore = new TrustScoreEntity { BusinessId = business.Id, OverallScore = 30 };
        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            VerificationType = "GST",
            Status = VerificationStatus.PENDING,
            FinalDecision = VerificationDecision.NONE
        };
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            DocumentType = "GST_CERTIFICATE",
            Status = DocumentStatus.PROCESSED
        };
        vCase.Evidences.Add(new VerificationEvidence { VerificationCaseId = vCase.Id, DocumentId = doc.Id, FieldName = "Gstin", ActualValue = "27AAACB1234F1Z5" });

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        context.TrustScores.Add(trustScore);
        context.VerificationCases.Add(vCase);
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var handler = new ApproveVerificationCommandHandler(context, calculator, currentUserServiceMock.Object);

        // First approval succeeds
        var firstResult = await handler.Handle(new ApproveVerificationCommand(vCase.Id, "First approval"), CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();
        var scoreAfterFirst = firstResult.Value.Supplier.RiskScore;

        // Second approval attempt on already approved case fails
        var secondResult = await handler.Handle(new ApproveVerificationCommand(vCase.Id, "Second approval"), CancellationToken.None);
        secondResult.IsSuccess.Should().BeFalse();
        secondResult.Error.Should().Contain("already been approved");

        // Score remains unchanged
        var dbScore = await context.TrustScores.FirstOrDefaultAsync(t => t.BusinessId == business.Id);
        dbScore!.OverallScore.Should().Be(scoreAfterFirst);
    }

    [Fact]
    public async Task OptionB_Deduplication_MultipleDocumentsOfSameType_ContributeOnlyOnce()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Vendor Org", Email = "vendor@test.com" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "Vendor Pvt Ltd",
            BusinessIdNumber = "VP-IN-2026-1234"
        };

        // Supplier uploads two versions/files of GST certificate
        var doc1 = new Document { Id = Guid.NewGuid(), BusinessId = business.Id, DocumentType = "GST_CERTIFICATE", Status = DocumentStatus.APPROVED };
        var doc2 = new Document { Id = Guid.NewGuid(), BusinessId = business.Id, DocumentType = "GST_CERTIFICATE", Status = DocumentStatus.APPROVED };

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        context.Documents.AddRange(doc1, doc2);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var calcResult = await calculator.CalculateAsync(business.Id);

        // Verification score should only count GST ONCE (25 points), NOT twice (50 points)
        calcResult.VerificationScore.Should().Be(25);
    }

    [Fact]
    public async Task RejectedOrEscalatedDocuments_DoNotContributeToScore()
    {
        using var context = CreateDbContext();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Vendor Org", Email = "vendor@test.com" };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            LegalName = "Vendor Pvt Ltd",
            BusinessIdNumber = "VP-IN-2026-1234"
        };

        var rejectedDoc = new Document { Id = Guid.NewGuid(), BusinessId = business.Id, DocumentType = "GST_CERTIFICATE", Status = DocumentStatus.REJECTED };
        var escalatedDoc = new Document { Id = Guid.NewGuid(), BusinessId = business.Id, DocumentType = "PAN_CARD", Status = DocumentStatus.PROCESSED };
        var pendingCase = new VerificationCase { Id = Guid.NewGuid(), BusinessId = business.Id, VerificationType = "PAN", Status = VerificationStatus.FLAGGED, FinalDecision = VerificationDecision.ESCALATED };

        context.Organizations.Add(org);
        context.Businesses.Add(business);
        context.Documents.AddRange(rejectedDoc, escalatedDoc);
        context.VerificationCases.Add(pendingCase);
        await context.SaveChangesAsync();

        var calculator = new TrustScoreCalculator(context);
        var calcResult = await calculator.CalculateAsync(business.Id);

        // Rejected and escalated items must not contribute
        calcResult.VerificationScore.Should().Be(0);
        calcResult.IdentityScore.Should().Be(0);
    }

    [Fact]
    public async Task ScoreThresholds_HighMediumLow_AndBuyerEligibility_AreAuthoritative()
    {
        // < 35 -> HIGH, ineligible
        SupplierRiskPolicy.DetermineRiskLevel(25).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.IsEligibleForBuyer(25).Should().BeFalse();

        SupplierRiskPolicy.DetermineRiskLevel(34.9).Should().Be(RiskLevel.HIGH);
        SupplierRiskPolicy.IsEligibleForBuyer(34.9).Should().BeFalse();

        // 35-70 -> MEDIUM
        SupplierRiskPolicy.DetermineRiskLevel(35.0).Should().Be(RiskLevel.MEDIUM);
        SupplierRiskPolicy.IsEligibleForBuyer(35.0).Should().BeFalse(); // <= 50 ineligible

        SupplierRiskPolicy.DetermineRiskLevel(50.0).Should().Be(RiskLevel.MEDIUM);
        SupplierRiskPolicy.IsEligibleForBuyer(50.0).Should().BeFalse(); // 50 not eligible

        SupplierRiskPolicy.DetermineRiskLevel(50.01).Should().Be(RiskLevel.MEDIUM);
        SupplierRiskPolicy.IsEligibleForBuyer(50.01).Should().BeTrue(); // > 50 eligible

        SupplierRiskPolicy.DetermineRiskLevel(70.0).Should().Be(RiskLevel.MEDIUM);
        SupplierRiskPolicy.IsEligibleForBuyer(70.0).Should().BeTrue();

        // > 70 -> LOW, eligible
        SupplierRiskPolicy.DetermineRiskLevel(70.01).Should().Be(RiskLevel.LOW);
        SupplierRiskPolicy.IsEligibleForBuyer(70.01).Should().BeTrue();

        SupplierRiskPolicy.DetermineRiskLevel(95.0).Should().Be(RiskLevel.LOW);
        SupplierRiskPolicy.IsEligibleForBuyer(95.0).Should().BeTrue();
    }
}
