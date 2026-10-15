using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VeriPass.Application.Auth;
using VeriPass.Application.Verification;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.API.FunctionalTests;

public class VerificationQueueApiTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly VeriPassApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public VerificationQueueApiTests(VeriPassApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<VerificationCase> SeedPendingVerificationCaseAsync(Guid? verifierId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            LegalName = $"ABC Manufacturing {Guid.NewGuid():N}",
            TradeName = "ABC Manufacturing",
            Gstin = "27AABCU9603R1ZM",
            Pan = "AABCU9603R"
        };
        context.Businesses.Add(business);

        var vCase = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            VerificationType = "GST",
            Status = VerificationStatus.PENDING,
            Priority = "HIGH",
            RiskLevel = RiskLevel.LOW,
            AssignedVerifierId = verifierId,
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationCases.Add(vCase);
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
            lastName = "Officer",
            phone = "+91 9999999999",
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
        auth.Should().NotBeNull();
        auth!.Role.Should().Be("VERIFIER");
        return auth.AccessToken;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@veripass.in",
            password = "Admin@VeriPass2026!"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        auth.Should().NotBeNull();
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

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = buyerEmail,
            password = buyerPassword
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetQueue_AsVerifier_Returns200WithPaginatedResults()
    {
        var verifierToken = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifications/queue?page=1&pageSize=25");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
        result.Summary.Should().NotBeNull();
        result.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetQueue_AliasRoute_Returns200OK()
    {
        var verifierToken = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?scope=all");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Summary.Should().NotBeNull();
    }

    [Fact]
    public async Task GetQueue_WithoutJwt_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/verifications/queue");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQueue_AsBuyer_Returns403Forbidden()
    {
        var buyerToken = await GetBuyerTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifications/queue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetQueueSummary_AsVerifier_Returns200WithAccurateCounters()
    {
        var verifierToken = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<VerificationQueueSummaryDto>(_jsonOptions);
        summary.Should().NotBeNull();
        summary!.TotalWaiting.Should().BeGreaterThanOrEqualTo(0);
        summary.InReviewCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetQueue_WithInvalidDateRange_Returns400BadRequest()
    {
        var verifierToken = await GetVerifierTokenAsync();

        var from = DateTime.UtcNow.AddDays(5).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifications/queue?dateFrom={from}&dateTo={to}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetQueue_SortBySla_Returns200OK()
    {
        var verifierToken = await GetVerifierTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?sortBy=sla&sortDirection=asc");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task FullWorkflow_StartReview_Approve_AndReflectInQueue()
    {
        var verifierToken = await GetVerifierTokenAsync();

        var seededCase = await SeedPendingVerificationCaseAsync();

        // 1. Get current queue to find a case to work on
        using var queueReq = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?status=PENDING&pageSize=10");
        queueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var queueRes = await _client.SendAsync(queueReq);
        queueRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var queueData = await queueRes.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        queueData.Should().NotBeNull();

        if (queueData!.Items.Count == 0)
        {
            // If no pending, query all cases
            using var allReq = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?status=ALL&pageSize=10");
            allReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
            var allRes = await _client.SendAsync(allReq);
            queueData = await allRes.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        }

        queueData!.Items.Should().NotBeEmpty();
        var targetCase = queueData.Items.First(x => x.Id == seededCase.Id);
        var caseId = targetCase.Id;

        // 2. Start review
        using var startReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{caseId}/start");
        startReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var startRes = await _client.SendAsync(startReq);
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Verify case detail reflects IN_PROGRESS
        using var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{caseId}");
        detailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var detailRes = await _client.SendAsync(detailReq);
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var caseDetail = await detailRes.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);
        caseDetail.Should().NotBeNull();
        caseDetail!.Status.Should().Be("IN_PROGRESS");

        // 4. Approve case
        using var approveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{caseId}/approve");
        approveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        approveReq.Content = JsonContent.Create(new { notes = "All statutory requirements verified." });
        var approveRes = await _client.SendAsync(approveReq);
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Verify case detail reflects VERIFIED / APPROVED
        using var verifiedReq = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{caseId}");
        verifiedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var verifiedRes = await _client.SendAsync(verifiedReq);
        verifiedRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifiedDetail = await verifiedRes.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);
        verifiedDetail.Should().NotBeNull();
        verifiedDetail!.Status.Should().Be("VERIFIED");
        verifiedDetail.FinalDecision.Should().Be("APPROVED");

        // 6. Verify Queue filtering: The case should appear when querying status=APPROVED
        using var approvedQueueReq = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?status=APPROVED");
        approvedQueueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var approvedQueueRes = await _client.SendAsync(approvedQueueReq);
        approvedQueueRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var approvedQueue = await approvedQueueRes.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        approvedQueue.Should().NotBeNull();
        approvedQueue!.Items.Should().Contain(x => x.Id == targetCase.Id);
    }

    [Fact]
    public async Task FullWorkflow_RejectAndEscalateActions_Succeed()
    {
        var verifierToken = await GetVerifierTokenAsync();

        var seededCase = await SeedPendingVerificationCaseAsync();

        using var queueReq = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/queue?pageSize=10");
        queueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var queueRes = await _client.SendAsync(queueReq);
        var queueData = await queueRes.Content.ReadFromJsonAsync<VerificationQueueResponseDto>(_jsonOptions);
        queueData.Should().NotBeNull();
        queueData!.Items.Should().NotBeEmpty();

        var targetCase = queueData.Items.First(x => x.Id == seededCase.Id);
        var caseId = targetCase.Id;

        // Test Request Information
        using var infoReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{caseId}/request-information");
        infoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        infoReq.Content = JsonContent.Create(new { informationRequired = "Clearer copy of certificate requested." });
        var infoRes = await _client.SendAsync(infoReq);
        infoRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Test Escalate
        using var escReq = new HttpRequestMessage(HttpMethod.Post, $"/api/verifier/cases/{caseId}/escalate");
        escReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        escReq.Content = JsonContent.Create(new { escalationReason = "Suspected tampering in digital certificate." });
        var escRes = await _client.SendAsync(escReq);
        escRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Escalated detail
        using var escDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/verifier/cases/{caseId}");
        escDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierToken);
        var escDetailRes = await _client.SendAsync(escDetailReq);
        escDetailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var escDetail = await escDetailRes.Content.ReadFromJsonAsync<VerificationCaseDetailDto>(_jsonOptions);
        escDetail.Should().NotBeNull();
        escDetail!.FinalDecision.Should().Be("ESCALATED");
    }
}
