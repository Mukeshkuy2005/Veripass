using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public class VerificationCaseWorkstationApiTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly VeriPassApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public VerificationCaseWorkstationApiTests(VeriPassApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<VerificationCase> SeedCaseAsync(
        string legalName = "ABC Manufacturing Pvt Ltd",
        string gstin = "09ABCDE1234F1Z5",
        string vType = "GST",
        VerificationStatus status = VerificationStatus.IN_PROGRESS,
        string priority = "HIGH")
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
            IsPrimary = true
        };
        context.BusinessAddresses.Add(address);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            DocumentType = "GST_CERTIFICATE",
            OriginalFileName = $"{legalName.Replace(" ", "_")}_GST.pdf",
            StorageKey = $"gst_cert_{Guid.NewGuid():N}.pdf",
            MimeType = "application/pdf",
            FileSize = 192840,
            Sha256Hash = "8b2f90a13c4d7e89123b56cf8912e45a789c01de23f45a67b89c01de23f45a67",
            Status = DocumentStatus.PROCESSED,
            ExtractionConfidence = 99.4m,
            UploadedAt = DateTime.UtcNow
        };
        context.Documents.Add(doc);

        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            VerificationType = vType,
            Status = status,
            Priority = priority,
            RiskLevel = RiskLevel.LOW,
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationCases.Add(vCase);

        var ev = new VerificationEvidence
        {
            Id = Guid.NewGuid(),
            VerificationCaseId = vCase.Id,
            DocumentId = doc.Id,
            EvidenceType = "PRIMARY_CREDENTIAL",
            FieldName = "Gstin",
            ExpectedValue = gstin,
            ActualValue = gstin,
            MatchStatus = "MATCH",
            ConfidenceScore = 99.8m,
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationEvidences.Add(ev);

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
        return vCase;
    }

    private async Task<string> GetVerifierTokenAsync()
    {
        var adminToken = await GetAdminTokenAsync();
        var verifierEmail = $"verifier_{Guid.NewGuid():N}@veripass.in";
        var verifierPassword = "VerifierPass123!";

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/verifiers");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        createMsg.Content = JsonContent.Create(new
        {
            email = verifierEmail,
            password = verifierPassword,
            firstName = "Compliance",
            lastName = "Workstation",
            phone = "+91 9888888888",
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
    public async Task GetCase_AsVerifier_ReturnsFullAuthoritativeWorkstationProjection()
    {
        // Arrange
        var vCase = await SeedCaseAsync("ABC Manufacturing Pvt Ltd", "09ABCDE1234F1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{vCase.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caseDto = await response.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);
        caseDto.Should().NotBeNull();
        caseDto!.CaseId.Should().Be(vCase.Id);
        caseDto.BusinessName.Should().Be("ABC Manufacturing Pvt Ltd");
        caseDto.Gstin.Should().Be("09ABCDE1234F1Z5");
        caseDto.Status.Should().Be("IN_PROGRESS");
        caseDto.Priority.Should().Be("HIGH");
        caseDto.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();

        // Workstation Sub-Projections
        caseDto.BusinessSummary.Should().NotBeNull();
        caseDto.BusinessSummary!.LegalName.Should().Be("ABC Manufacturing Pvt Ltd");
        caseDto.BusinessSummary.Address.Should().Contain("Sector 12");
        caseDto.BusinessSummary.City.Should().Be("Ghaziabad");
        caseDto.BusinessSummary.Pincode.Should().Be("201014");

        caseDto.EvidenceDocuments.Should().NotBeNullOrEmpty();
        caseDto.EvidenceDocuments![0].DownloadUrl.Should().Contain("/api/v1/documents/download/");

        caseDto.Extraction.Should().NotBeNull();
        caseDto.Extraction!.Status.Should().Be("COMPLETED");
        caseDto.Extraction.Fields.Should().NotBeEmpty();

        caseDto.TrustedSource.Should().NotBeNull();
        caseDto.TrustedSource!.Provider.Should().Be("GSTN");

        caseDto.MatchAnalysis.Should().NotBeNullOrEmpty();
        caseDto.MatchAnalysis!.Any(m => m.MatchStatus == "MATCH").Should().BeTrue();

        caseDto.RiskAssessment.Should().NotBeNull();
        caseDto.RiskAssessment!.Factors.Should().NotBeNull();

        caseDto.AvailableActions.Should().Contain("APPROVE");
        caseDto.AvailableActions.Should().Contain("REJECT");
    }

    [Fact]
    public async Task GetCase_AsBuyer_ReturnsForbidden()
    {
        // Arrange
        var vCase = await SeedCaseAsync();
        var buyerToken = await GetBuyerTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{vCase.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApproveCase_WithValidConcurrencyToken_SucceedsAndPersistsAudit()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Omega Instruments Ltd", "27AABCU9603R1ZM", "GST");
        var token = await GetVerifierTokenAsync();

        // Fetch current concurrency token
        using var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{vCase.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await _client.SendAsync(getReq);
        var initialDto = await getRes.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);
        var concurrencyToken = long.Parse(initialDto!.ConcurrencyToken!);

        // Act: Approve with valid concurrency token
        using var approveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/approve");
        approveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        approveReq.Content = JsonContent.Create(new
        {
            notes = "Authoritative statutory match confirmed across sovereign GSTN portal.",
            concurrencyToken = concurrencyToken
        });
        var approveRes = await _client.SendAsync(approveReq);

        // Assert
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Database State
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCase = await context.VerificationCases.FindAsync(vCase.Id);
        updatedCase.Should().NotBeNull();
        updatedCase!.Status.Should().Be(VerificationStatus.VERIFIED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.APPROVED);
        updatedCase.DecisionReason.Should().Contain("sovereign GSTN portal");

        // Verify Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == vCase.Id.ToString() && a.Action == "VERIFICATION_APPROVED");
        audit.Should().NotBeNull();
        audit!.Result.Should().Be("SUCCESS");
    }

    [Fact]
    public async Task ApproveCase_WithStaleConcurrencyToken_ReturnsConflictError()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Beta Industries Ltd", "06AAAAA0000A1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        // Act: Attempt to approve with outdated concurrency token
        using var approveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/approve");
        approveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        approveReq.Content = JsonContent.Create(new
        {
            notes = "Approval with stale token",
            concurrencyToken = 999999999L // Outdated token
        });
        var approveRes = await _client.SendAsync(approveReq);

        // Assert
        approveRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await approveRes.Content.ReadAsStringAsync();
        content.Should().Contain("modified by another user or session");

        // Verify Database State Unchanged
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unaffectedCase = await context.VerificationCases.FindAsync(vCase.Id);
        unaffectedCase!.FinalDecision.Should().Be(VerificationDecision.NONE);
    }

    [Fact]
    public async Task RejectCase_RequiresReason_AndPersistsRejectionAudit()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Delta Corp", "33BBBBB1111B1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        // Act: Reject
        using var rejectReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/reject");
        rejectReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        rejectReq.Content = JsonContent.Create(new
        {
            reason = "Entity legal name does not match sovereign registry."
        });
        var rejectRes = await _client.SendAsync(rejectReq);

        // Assert
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCase = await context.VerificationCases.FindAsync(vCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.REJECTED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.REJECTED);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == vCase.Id.ToString() && a.Action == "VERIFICATION_REJECTED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestInformation_PersistsRequestedDetailsAndAudit()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Gamma Logistics", "07CCCCC2222C1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        // Act
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/request-information");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new
        {
            informationRequired = "Please provide an unredacted copy of Form GST REG-06."
        });
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCase = await context.VerificationCases.FindAsync(vCase.Id);
        updatedCase!.FinalDecision.Should().Be(VerificationDecision.REQUEST_MORE_INFO);
        updatedCase.DecisionReason.Should().Contain("Form GST REG-06");

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == vCase.Id.ToString() && a.Action == "VERIFICATION_MORE_INFO_REQUESTED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task EscalateCase_SetsEscalatedStatusAndHighPriority()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Zeta Polymers", "19DDDDD3333D1Z5", "GST", priority: "MEDIUM");
        var token = await GetVerifierTokenAsync();

        // Act
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/escalate");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new
        {
            escalationReason = "Conflicting addresses across GSTN and MCA registers requires legal officer review."
        });
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCase = await context.VerificationCases.FindAsync(vCase.Id);
        updatedCase!.Status.Should().Be(VerificationStatus.FLAGGED);
        updatedCase.FinalDecision.Should().Be(VerificationDecision.ESCALATED);
        updatedCase.Priority.Should().Be("HIGH");

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == vCase.Id.ToString() && a.Action == "VERIFICATION_ESCALATED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifySource_ExecutesSovereignRegistryCheckAndPersistsRecord()
    {
        // Arrange
        var vCase = await SeedCaseAsync("Epsilon Biotech", "29EEEEE4444E1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        // Act
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{vCase.Id}/verify-source");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var trustedSource = await res.Content.ReadFromJsonAsync<CaseTrustedSourceDto>(_jsonOptions);
        trustedSource.Should().NotBeNull();
        trustedSource!.Provider.Should().NotBeNullOrWhiteSpace();

        // Check persisted VerificationCheck entity
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var check = await context.VerificationChecks.FirstOrDefaultAsync(c => c.VerificationCaseId == vCase.Id);
        check.Should().NotBeNull();
        check!.RequestReference.Should().NotBeNullOrWhiteSpace();

        // Check Audit Log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == vCase.Id.ToString() && a.Action == "VERIFICATION_SOURCE_CHECKED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiCase_DataIsolation_PreventsCrossCaseLeakage()
    {
        // Arrange: Seed two distinct cases
        var caseA = await SeedCaseAsync("Entity Alpha Pvt Ltd", "09AAAAA1111A1Z5", "GST");
        var caseB = await SeedCaseAsync("Entity Beta Pvt Ltd", "27BBBBB2222B1Z5", "GST");
        var token = await GetVerifierTokenAsync();

        // Act: Fetch Case A
        using var reqA = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{caseA.Id}");
        reqA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resA = await _client.SendAsync(reqA);
        var dtoA = await resA.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);

        // Act: Fetch Case B
        using var reqB = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{caseB.Id}");
        reqB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resB = await _client.SendAsync(reqB);
        var dtoB = await resB.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);

        // Assert Strict Data Isolation
        dtoA!.BusinessName.Should().Be("Entity Alpha Pvt Ltd");
        dtoA.Gstin.Should().Be("09AAAAA1111A1Z5");
        dtoA.BusinessName.Should().NotContain("Entity Beta");
        dtoA.Gstin.Should().NotContain("27BBBBB");

        dtoB!.BusinessName.Should().Be("Entity Beta Pvt Ltd");
        dtoB.Gstin.Should().Be("27BBBBB2222B1Z5");
        dtoB.BusinessName.Should().NotContain("Entity Alpha");
        dtoB.Gstin.Should().NotContain("09AAAAA");
    }
}
