using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeriPass.Application.Auth;
using VeriPass.Application.Verification;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.API.FunctionalTests;

public class DocumentReviewWorkstationApiTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly VeriPassApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DocumentReviewWorkstationApiTests(VeriPassApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(VerificationCase Case, Document Document)> SeedCaseWithEvidenceAsync(
        string legalName = "ABC Manufacturing Pvt Ltd",
        string gstin = "09ABCDE1234F1Z5",
        string vType = "GST",
        string docFileName = "GST_Certificate.pdf")
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = legalName,
            TradeName = legalName,
            Gstin = gstin,
            Pan = gstin.Length >= 12 ? gstin.Substring(2, 10) : "ABCDE1234F",
            BusinessIdNumber = "U72200DL2021PTC123456",
            CreatedAt = DateTime.UtcNow
        };
        context.Businesses.Add(business);

        var address = new BusinessAddress
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            AddressType = "REGISTERED",
            Line1 = "Plot 42, Sector 12 Industrial Area",
            City = "Ghaziabad",
            State = "Uttar Pradesh",
            Pincode = "201014",
            Country = "India",
            CreatedAt = DateTime.UtcNow
        };
        context.BusinessAddresses.Add(address);

        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            VerificationType = vType,
            Status = VerificationStatus.IN_PROGRESS,
            Priority = "HIGH",
            FinalDecision = VerificationDecision.NONE,
            RiskLevel = RiskLevel.LOW,
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationCases.Add(vCase);

        var storageKey = $"documents/{business.Id}/{vCase.Id}/{docFileName}";
        var sampleBytes = VeriPass.Application.Common.Helpers.PdfCertificateGenerator.GenerateCertificatePdf(
            vType, legalName, legalName, gstin, "Plot 42, Sector 12 Industrial Area", storageKey);
        var authoritativeHash = Convert.ToHexString(SHA256.HashData(sampleBytes)).ToLowerInvariant();

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            DocumentType = vType,
            OriginalFileName = docFileName,
            MimeType = "application/pdf",
            FileSize = sampleBytes.Length,
            StorageKey = storageKey,
            Sha256Hash = authoritativeHash,
            CurrentVersion = 1,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        context.Documents.Add(doc);

        var evidence = new VerificationEvidence
        {
            Id = Guid.NewGuid(),
            VerificationCaseId = vCase.Id,
            DocumentId = doc.Id,
            EvidenceType = $"{vType}_CERTIFICATE",
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationEvidences.Add(evidence);

        var check = new VerificationCheck
        {
            Id = Guid.NewGuid(),
            VerificationCaseId = vCase.Id,
            Provider = "GSTN",
            CheckType = "REGISTRY_CONFIRMATION",
            Status = VerificationStatus.VERIFIED,
            RequestReference = $"SOV-REQ-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            ConfidenceScore = 99.5m,
            PerformedAt = DateTime.UtcNow
        };
        context.VerificationChecks.Add(check);

        await context.SaveChangesAsync();
        return (vCase, doc);
    }

    private async Task<string> GetVerifierTokenAsync()
    {
        var adminToken = await GetAdminTokenAsync();
        var verifierEmail = $"verifier_doc_{Guid.NewGuid():N}@veripass.in";
        var verifierPassword = "VerifierPass123!";

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/verifiers");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        createMsg.Content = JsonContent.Create(new
        {
            email = verifierEmail,
            password = verifierPassword,
            firstName = "Compliance",
            lastName = "Officer",
            phone = "+91 9777777777",
            mustChangePassword = false
        });

        var createRes = await _client.SendAsync(createMsg);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = verifierEmail,
            password = verifierPassword
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@veripass.in",
            password = "Admin@VeriPass2026!"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> GetBuyerTokenAsync()
    {
        var buyerEmail = $"buyer_{Guid.NewGuid():N}@buyercompany.com";
        var buyerPassword = "BuyerSecurePass123!";

        var registerRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = buyerEmail,
            password = buyerPassword,
            firstName = "Procurement",
            lastName = "Lead",
            organizationName = $"Buyer Org {Guid.NewGuid():N}",
            organizationType = "BUYER",
            role = "BUYER"
        });
        registerRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = buyerEmail,
            password = buyerPassword
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetDocumentReview_AsVerifier_ReturnsFullAuthoritativeReviewProjection()
    {
        // Arrange
        var (vCase, doc) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/document");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await response.Content.ReadFromJsonAsync<DocumentReviewDetailDto>(_jsonOptions);
        review.Should().NotBeNull();
        review!.CaseId.Should().Be(vCase.Id);
        review.BusinessName.Should().Be("ABC Manufacturing Pvt Ltd");
        review.LegalName.Should().Be("ABC Manufacturing Pvt Ltd");
        review.CaseStatus.Should().Be("IN_PROGRESS");

        // Document Details (Section 15)
        review.Document.Should().NotBeNull();
        review.Document.Id.Should().Be(doc.Id);
        review.Document.FileName.Should().Be("GST_Certificate.pdf");
        review.Document.VersionNumber.Should().Be(1);
        review.Document.Sha256Hash.Should().NotBeNullOrWhiteSpace();
        review.Document.HashMatchStatus.Should().Be("MATCH");
        review.Document.DownloadUrl.Should().Contain($"/cases/{vCase.Id}/documents/{doc.Id}/download");
        review.Document.ContentUrl.Should().Contain($"/cases/{vCase.Id}/documents/{doc.Id}/content");

        // OCR Results (Section 20-25)
        review.Ocr.Should().NotBeNull();
        review.Ocr.Status.Should().Be("COMPLETED");
        review.Ocr.EngineName.Should().Contain("OCR");
        review.Ocr.Fields.Should().NotBeEmpty();
        review.Ocr.Fields.Should().Contain(f => f.Confidence >= 90m);
        review.Ocr.RawOcrText.Should().NotBeNullOrWhiteSpace();

        // Validation Results (Section 26-29)
        review.Validation.Should().NotBeNull();
        review.Validation.Items.Should().NotBeEmpty();
        review.Validation.Items.Should().Contain(v => v.ValidationType == "FILE_FORMAT" && v.Status == "PASS");
        review.Validation.Items.Should().Contain(v => v.ValidationType == "REQUIRED_FIELDS" && v.Status == "PASS");
        review.Validation.Items.Should().Contain(v => v.ValidationType == "FIELD_FORMAT" && v.Status == "PASS");

        // Document Integrity (Section 33-36)
        review.Integrity.Should().NotBeNull();
        review.Integrity.Status.Should().Be("NO_ANOMALIES_DETECTED");
        review.Integrity.RiskLevel.Should().Be("LOW");
        review.Integrity.DefensibleFinding.Should().Contain("No detected integrity anomalies");
        review.Integrity.Checks.Should().Contain(c => c.Contains("file hash", StringComparison.OrdinalIgnoreCase));

        // Evidence Provenance (Section 51-53)
        review.Provenance.Should().NotBeNull();
        review.Provenance.CaseId.Should().Be(vCase.Id.ToString());
        review.Provenance.DocumentId.Should().Be(doc.Id.ToString());
        review.Provenance.VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetDocumentReview_DoesNotChangeCaseStatus()
    {
        // Arrange
        var (vCase, _) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/document");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Case status in database must remain unmodified
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedCase = await context.VerificationCases.FindAsync(vCase.Id);
        persistedCase.Should().NotBeNull();
        persistedCase!.Status.Should().Be(VerificationStatus.IN_PROGRESS);
        persistedCase.FinalDecision.Should().Be(VerificationDecision.NONE);
    }

    [Fact]
    public async Task PreventIdor_DocumentFromAnotherCase_Returns403Or404()
    {
        // Arrange
        var (caseA, _) = await SeedCaseWithEvidenceAsync("Company A", "09AAAAA1111A1Z1", "GST", "Cert_A.pdf");
        var (_, docB) = await SeedCaseWithEvidenceAsync("Company B", "09BBBBB2222B2Z2", "GST", "Cert_B.pdf");
        var token = await GetVerifierTokenAsync();

        // Attempt to request docB under caseA
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{caseA.Id}/document?documentId={docB.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: IDOR strictly forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDocumentContent_StreamsValidPdfBytes()
    {
        // Arrange
        var (vCase, doc) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/documents/{doc.Id}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();

        // Verify PDF Header Magic Bytes (%PDF)
        var header = System.Text.Encoding.ASCII.GetString(bytes[..4]);
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task DownloadDocument_StreamsValidPdfWithAttachmentHeader()
    {
        // Arrange
        var (vCase, doc) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/documents/{doc.Id}/download");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition.Should().NotBeNull();
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Contain("GST_Certificate.pdf");
    }

    [Fact]
    public async Task ReprocessOcr_AsVerifier_UpdatesExtractionAndLogsAudit()
    {
        // Arrange
        var (vCase, doc) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/verifier/cases/{vCase.Id}/documents/{doc.Id}/ocr");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ocr = await response.Content.ReadFromJsonAsync<OcrReviewDto>(_jsonOptions);
        ocr.Should().NotBeNull();
        ocr!.Status.Should().Be("COMPLETED");
        ocr.Fields.Should().NotBeEmpty();

        // Verify Audit Log was recorded
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await context.AuditLogs
            .Where(a => a.ResourceId == doc.Id.ToString() && a.Action == "DOCUMENT_OCR_REPROCESSED")
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateDocumentIntegrity_AsVerifier_ReturnsIntegrityResultAndLogsAudit()
    {
        // Arrange
        var (vCase, doc) = await SeedCaseWithEvidenceAsync();
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/verifier/cases/{vCase.Id}/documents/{doc.Id}/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var integrity = await response.Content.ReadFromJsonAsync<DocumentIntegrityReviewDto>(_jsonOptions);
        integrity.Should().NotBeNull();
        integrity!.Status.Should().Be("NO_ANOMALIES_DETECTED");
        integrity.RiskLevel.Should().Be("LOW");

        // Verify Audit Log was recorded
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = await context.AuditLogs
            .Where(a => a.ResourceId == doc.Id.ToString() && a.Action == "DOCUMENT_INTEGRITY_CHECKED")
            .FirstOrDefaultAsync();
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDocumentReview_WhenHashMismatched_DetectsAnomalyAndFlagsWarning()
    {
        // Arrange: seed document with altered/manipulated SHA256 that does not match physical bytes
        var (vCase, doc) = await SeedCaseWithEvidenceAsync("Tampered Document Entity", "09TAMPER1234F1Z1", "GST", "Tampered_Cert.pdf");

        // Alter the persisted hash directly in DB to simulate mismatch
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedDoc = await context.Documents.FindAsync(doc.Id);
            persistedDoc!.Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; // Empty file hash
            await context.SaveChangesAsync();
        }

        var token = await GetVerifierTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/document");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Mismatch must be detected
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = await response.Content.ReadFromJsonAsync<DocumentReviewDetailDto>(_jsonOptions);
        review.Should().NotBeNull();
        review!.Document.HashMatchStatus.Should().Be("MISMATCH");
        review.Integrity.Status.Should().Be("ANOMALIES_DETECTED");
        review.Validation.Items.Should().Contain(v => v.ValidationType == "FILE_INTEGRITY" && v.Status == "FAIL");
    }

    [Fact]
    public async Task GetDocumentReview_AsBuyer_Returns403Forbidden()
    {
        // Arrange
        var (vCase, _) = await SeedCaseWithEvidenceAsync();
        var buyerToken = await GetBuyerTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/cases/{vCase.Id}/document");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
