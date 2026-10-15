using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VeriPass.Infrastructure.DocumentIntelligence;

namespace VeriPass.Infrastructure.IntegrationTests;

public class OcrExtractionServiceTests
{
    private readonly DocumentExtractionService _extractionService;

    public OcrExtractionServiceTests()
    {
        var ocrService = new OcrService(NullLogger<OcrService>.Instance);
        var tamperService = new DocumentTamperDetectionService(NullLogger<DocumentTamperDetectionService>.Instance);
        _extractionService = new DocumentExtractionService(ocrService, tamperService);
    }

    [Fact]
    public async Task ExtractGstCertificate_ShouldExtractGstinAndDerivePan()
    {
        var sampleGstText = @"
            GOVERNMENT OF INDIA
            FORM GST REG-06
            Registration Certificate
            Registration Number: 27AABCS1429B1ZB
            Legal Name: SUPREME INDUSTRIAL FASTENERS PRIVATE LIMITED
            Trade Name: SUPREME FASTENERS
            Date of Liability: 01/07/2017
        ";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleGstText));

        var result = await _extractionService.ExtractAndAnalyzeAsync(stream, "GST_CERTIFICATE");

        result.Success.Should().BeTrue();
        result.Confidence.Should().BeGreaterThan(0.9m);
        result.Fields.Should().ContainKey("Gstin").WhoseValue.Should().Be("27AABCS1429B1ZB");
        result.Fields.Should().ContainKey("Pan").WhoseValue.Should().Be("AABCS1429B");
        result.Fields.Should().ContainKey("LegalName");
    }

    [Fact]
    public async Task ExtractPanCard_ShouldExtractValidPanPattern()
    {
        var samplePanText = @"
            INCOME TAX DEPARTMENT
            GOVT. OF INDIA
            Permanent Account Number Card
            AAACA9999F
            APEX POLYMERS PRIVATE LIMITED
            12/04/2015
        ";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(samplePanText));

        var result = await _extractionService.ExtractAndAnalyzeAsync(stream, "PAN_CARD");

        result.Success.Should().BeTrue();
        result.Fields.Should().ContainKey("Pan").WhoseValue.Should().Be("AAACA9999F");
    }

    [Fact]
    public async Task ExtractUdyamCertificate_ShouldExtractUdyamRegistrationNumber()
    {
        var sampleUdyamText = @"
            MINISTRY OF MICRO, SMALL & MEDIUM ENTERPRISES
            UDYAM REGISTRATION CERTIFICATE
            UDYAM-MH-12-0054321
            NAME OF ENTERPRISE: TECHNO METAL WORKS
            MAJOR ACTIVITY: SERVICES
        ";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleUdyamText));

        var result = await _extractionService.ExtractAndAnalyzeAsync(stream, "UDYAM_CERT");

        result.Success.Should().BeTrue();
        result.Fields.Should().ContainKey("UdyamNumber").WhoseValue.Should().Be("UDYAM-MH-12-0054321");
    }

    [Fact]
    public async Task ExtractCancelledCheque_ShouldExtractIfscAndBankDetails()
    {
        var sampleChequeText = @"
            STATE BANK OF INDIA
            IFSC: SBIN0001234
            CANCELLED
            PAY: ABC INDUSTRIAL DYNAMICS
            A/C NO: 123456789012
        ";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleChequeText));

        var result = await _extractionService.ExtractAndAnalyzeAsync(stream, "CANCELLED_CHEQUE");

        result.Success.Should().BeTrue();
        result.Fields.Should().ContainKey("Ifsc").WhoseValue.Should().Be("SBIN0001234");
        result.Fields.Should().ContainKey("DocumentType");
    }
}
