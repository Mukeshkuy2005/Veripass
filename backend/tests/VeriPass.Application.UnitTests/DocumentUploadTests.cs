using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Documents;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;

namespace VeriPass.Application.UnitTests;

public class DocumentUploadTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UploadDocument_ShouldExecuteCompleteKillerWorkflow_FromStorageToDbAndOcr()
    {
        using var context = CreateDbContext();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Apex Polymers Pvt Ltd",
            BusinessIdNumber = "VP-IN-2026-9999"
        };
        context.Businesses.Add(business);

        var credType = new CredentialType
        {
            Id = Guid.NewGuid(),
            Code = "GSTIN",
            Name = "Goods and Services Tax Identification Number",
            Category = "TAX",
            VerificationProvider = "GSTN",
            RequiresExpiry = false,
            IsRequired = true
        };
        context.CredentialTypes.Add(credType);
        await context.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageService>();
        storageMock.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("documents/apex/gst_cert_2026.pdf", "sha256_mock_hash", 2048L));

        var ocrMock = new Mock<IDocumentIntelligenceService>();
        ocrMock.Setup(o => o.ExtractAndAnalyzeAsync(It.IsAny<Stream>(), "GST_CERTIFICATE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentExtractionResult
            {
                Success = true,
                Confidence = 0.98m,
                Fields = new Dictionary<string, string>
                {
                    ["Gstin"] = "27AAACA1234F1Z5",
                    ["LegalName"] = "Apex Polymers Pvt Ltd",
                    ["RegistrationDate"] = "2017-07-01"
                }
            });

        var providerMock = new Mock<IVerificationProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("GSTN");
        providerMock.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryVerificationResult
            {
                IsValid = true,
                Status = VerificationStatus.VERIFIED,
                ConfidenceScore = 98m,
                Provider = "GSTN",
                ReferenceNumber = "GSTN-REF-2026-99"
            });

        var providerFactoryMock = new Mock<IVerificationProviderFactory>();
        providerFactoryMock.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(providerMock.Object);

        var trustCalculatorMock = new Mock<ITrustScoreCalculator>();
        trustCalculatorMock.Setup(t => t.CalculateAsync(business.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrustScoreCalculationResult
            {
                OverallScore = 88,
                IdentityScore = 90,
                CredentialScore = 85,
                ComplianceScore = 85,
                VerificationScore = 90,
                RiskScore = 95,
                Version = "v1.0"
            });

        var handler = new UploadDocumentCommandHandler(
            context,
            storageMock.Object,
            ocrMock.Object,
            providerFactoryMock.Object,
            trustCalculatorMock.Object);

        var sampleFileBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock GST Certificate Content for Apex Polymers");
        using var fileStream = new MemoryStream(sampleFileBytes);

        var command = new UploadDocumentCommand(
            business.Id,
            "GST_CERTIFICATE",
            "gst_certificate.pdf",
            "application/pdf",
            fileStream,
            Guid.NewGuid()
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Killer Workflow Verification (Section 59)
        result.IsSuccess.Should().BeTrue();
        result.Value.DocumentId.Should().NotBeEmpty();
        result.Value.ExtractedFields.Should().ContainKey("Gstin").WhoseValue.Should().Be("27AAACA1234F1Z5");
        result.Value.NewTrustScore.Should().Be(88);
        result.Value.VerificationStatus.Should().Be("VERIFIED");

        // 1. Storage Upload was called
        storageMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "gst_certificate.pdf", "application/pdf", It.IsAny<CancellationToken>()), Times.Once);

        // 2. Database records verified
        var savedDoc = await context.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == result.Value.DocumentId);
        savedDoc.Should().NotBeNull();
        savedDoc!.BusinessId.Should().Be(business.Id);
        savedDoc.DocumentType.Should().Be("GST_CERTIFICATE");
        savedDoc.StorageKey.Should().Be("documents/apex/gst_cert_2026.pdf");
        savedDoc.Versions.Should().HaveCount(1);
        savedDoc.Versions.First().VersionNumber.Should().Be(1);

        // 3. OCR extraction was executed
        ocrMock.Verify(o => o.ExtractAndAnalyzeAsync(It.IsAny<Stream>(), "GST_CERTIFICATE", It.IsAny<CancellationToken>()), Times.Once);
        savedDoc.ExtractedDataJson.Should().Contain("27AAACA1234F1Z5");

        // 4. Registry Verification Provider was triggered
        providerMock.Verify(p => p.VerifyAsync("27AAACA1234F1Z5", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);

        // 5. Verification Case and Check created
        var vCase = await context.VerificationCases.FirstOrDefaultAsync(v => v.BusinessId == business.Id);
        vCase.Should().NotBeNull();
        vCase!.FinalDecision.Should().Be(VerificationDecision.APPROVED);

        var vCheck = await context.VerificationChecks.FirstOrDefaultAsync(c => c.VerificationCaseId == vCase.Id);
        vCheck.Should().NotBeNull();
        vCheck!.Provider.Should().Be("GSTN");

        // 6. Trust score calculator was triggered & Snapshot created with calculation version
        trustCalculatorMock.Verify(t => t.CalculateAsync(business.Id, It.IsAny<CancellationToken>()), Times.Once);

        var snapshot = await context.TrustScoreSnapshots.FirstOrDefaultAsync(s => s.BusinessId == business.Id);
        snapshot.Should().NotBeNull();
        snapshot!.OverallScore.Should().Be(88);
        snapshot.CalculationVersion.Should().Be("v1.0");
    }

    [Fact]
    public async Task UploadDocument_WhenTamperingDetected_ShouldCreateHighRiskSignal()
    {
        using var context = CreateDbContext();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = "Suspicious Shell Co",
            BusinessIdNumber = "VP-IN-2026-0001"
        };
        context.Businesses.Add(business);
        await context.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageService>();
        storageMock.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("documents/suspicious/pan.pdf", "sha256_hash", 1024L));

        var ocrMock = new Mock<IDocumentIntelligenceService>();
        ocrMock.Setup(o => o.ExtractAndAnalyzeAsync(It.IsAny<Stream>(), "PAN_CARD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentExtractionResult
            {
                Success = true,
                Confidence = 0.65m,
                HasTamperingSigns = true,
                TamperingDetails = "Font inconsistency detected around PAN text field",
                Fields = new Dictionary<string, string> { ["Pan"] = "ABCDE1234F" }
            });

        var providerMock = new Mock<IVerificationProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("NSDL");
        providerMock.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryVerificationResult
            {
                IsValid = false,
                Status = VerificationStatus.REJECTED,
                ConfidenceScore = 50m,
                Provider = "NSDL"
            });

        var providerFactoryMock = new Mock<IVerificationProviderFactory>();
        providerFactoryMock.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(providerMock.Object);

        var trustCalculatorMock = new Mock<ITrustScoreCalculator>();
        trustCalculatorMock.Setup(t => t.CalculateAsync(business.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrustScoreCalculationResult { OverallScore = 35, RiskScore = 20, Version = "v1.0" });

        var handler = new UploadDocumentCommandHandler(
            context,
            storageMock.Object,
            ocrMock.Object,
            providerFactoryMock.Object,
            trustCalculatorMock.Object);

        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("Fake pan pdf"));
        var command = new UploadDocumentCommand(business.Id, "PAN_CARD", "pan.pdf", "application/pdf", fileStream, Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var signal = await context.RiskSignals.FirstOrDefaultAsync(r => r.BusinessId == business.Id && r.SignalType == "DOCUMENT_TAMPERING");
        signal.Should().NotBeNull();
        signal!.Severity.Should().Be(RiskLevel.HIGH);
        signal.ScoreImpact.Should().Be(-25);
    }
}
