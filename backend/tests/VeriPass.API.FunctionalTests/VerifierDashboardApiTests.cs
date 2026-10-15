using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VeriPass.Application.Auth;
using VeriPass.Application.Verification;
using Xunit;

namespace VeriPass.API.FunctionalTests;

public class VerifierDashboardApiTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public VerifierDashboardApiTests(VeriPassApiFactory factory)
    {
        _client = factory.CreateClient();
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
        auth!.Role.Should().Be("ADMIN");
        return auth.AccessToken;
    }

    private async Task<string> GetSupplierTokenAsync()
    {
        var email = $"supplier_{Guid.NewGuid():N}@veripass.test";
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = email,
            password = "Password123!",
            firstName = "Regular",
            lastName = "Supplier",
            organizationName = "Test Supplier Org",
            organizationType = 0 // SUPPLIER
        });

        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetVerifierDashboard_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/verifier/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var legacyResponse = await _client.GetAsync("/api/verifier/dashboard");
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVerifierDashboard_WithSupplierRole_Returns403Forbidden()
    {
        var token = await GetSupplierTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifier/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVerifierDashboard_WithVerifierToken_Returns200OkAndValidStructure()
    {
        var token = await GetVerifierTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifier/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await response.Content.ReadFromJsonAsync<VerifierDashboardResponseDto>(_jsonOptions);
        dashboard.Should().NotBeNull();
        dashboard!.Summary.Should().NotBeNull();
        dashboard.PriorityCases.Should().NotBeNull();
        dashboard.Performance.Should().NotBeNull();
        dashboard.RiskDistribution.Should().NotBeNull();
        dashboard.RecentDecisions.Should().NotBeNull();

        // Validate structure fields are populated from database
        dashboard.Performance.ApprovalRate.Should().BeInRange(0.0, 100.0);
        dashboard.Performance.EscalationRate.Should().BeInRange(0.0, 100.0);
    }

    [Fact]
    public async Task GetVerifierDashboard_WithAdminToken_Returns200OkAndValidStructure()
    {
        var token = await GetAdminTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/verifier/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await response.Content.ReadFromJsonAsync<VerifierDashboardResponseDto>(_jsonOptions);
        dashboard.Should().NotBeNull();
        dashboard!.Summary.Should().NotBeNull();
    }
}
