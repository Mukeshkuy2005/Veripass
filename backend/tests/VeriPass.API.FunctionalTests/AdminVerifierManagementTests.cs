using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VeriPass.Application.Admin;
using VeriPass.Application.Auth;

namespace VeriPass.API.FunctionalTests;

public class AdminVerifierManagementTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AdminVerifierManagementTests(VeriPassApiFactory factory)
    {
        _client = factory.CreateClient();
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

    [Fact]
    public async Task AdminEndpoint_WithoutToken_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/v1/admin/verifiers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonAdminUser_AccessingAdminVerifiers_ShouldReturn403Forbidden()
    {
        // 1. Register a regular supplier
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"supplier_{Guid.NewGuid():N}@example.com",
            password = "Password123!",
            firstName = "Regular",
            lastName = "Supplier",
            organizationName = "Regular Supplier Org",
            organizationType = 0 // SUPPLIER
        });

        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        auth.Should().NotBeNull();

        // 2. Attempt to call admin verifiers endpoint
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/verifiers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicRegistration_WithAdminOrVerifierType_ShouldBeRejected()
    {
        // Attempting to self-register with invalid organization types outside supplier/buyer
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "fakeadmin@example.com",
            password = "Password123!",
            firstName = "Fake",
            lastName = "Admin",
            organizationName = "Fake Org",
            organizationType = 2 // Invalid type for self registration
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_CanCreate_List_AndManage_VerifierLifecycle()
    {
        var adminToken = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. Create a Verifier
        var verifierEmail = $"verifier_{Guid.NewGuid():N}@veripass.in";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/admin/verifiers", new
        {
            email = verifierEmail,
            password = "VerifierInitialPass2026!",
            firstName = "Meera",
            lastName = "Nair",
            phone = "+91 9876543210",
            mustChangePassword = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdVerifier = await createResponse.Content.ReadFromJsonAsync<AdminVerifierSummaryDto>(_jsonOptions);
        createdVerifier.Should().NotBeNull();
        createdVerifier!.Email.Should().Be(verifierEmail.ToLowerInvariant());
        createdVerifier.Status.Should().Be("ACTIVE");
        createdVerifier.MustChangePassword.Should().BeTrue();

        var verifierId = createdVerifier.Id;

        // 2. List Verifiers and verify created verifier is returned
        var listResponse = await _client.GetAsync("/api/v1/admin/verifiers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listData = await listResponse.Content.ReadFromJsonAsync<AdminVerifiersPagedResponse>(_jsonOptions);
        listData.Should().NotBeNull();
        listData!.Items.Should().Contain(v => v.Id == verifierId);
        listData.Stats.TotalVerifiers.Should().BeGreaterThan(0);

        // 3. Verifier can log in with provisioned credentials
        _client.DefaultRequestHeaders.Authorization = null;
        var verifierLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = verifierEmail,
            password = "VerifierInitialPass2026!"
        });

        verifierLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifierAuth = await verifierLoginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        verifierAuth.Should().NotBeNull();
        verifierAuth!.Role.Should().Be("VERIFIER");
        verifierAuth.MustChangePassword.Should().BeTrue();

        // 4. Verifier changes their initial password
        using (var changePassReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password"))
        {
            changePassReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", verifierAuth.AccessToken);
            changePassReq.Content = JsonContent.Create(new
            {
                currentPassword = "VerifierInitialPass2026!",
                newPassword = "VerifierSecureNewPass2026!"
            });
            var changePassRes = await _client.SendAsync(changePassReq);
            changePassRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 5. Admin suspends verifier
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var suspendResponse = await _client.PostAsJsonAsync($"/api/v1/admin/verifiers/{verifierId}/suspend", new
        {
            reason = "Auditing compliance credentials"
        });
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Suspended verifier cannot log in
        _client.DefaultRequestHeaders.Authorization = null;
        var suspendedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = verifierEmail,
            password = "VerifierSecureNewPass2026!"
        });
        suspendedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 7. Admin reactivates verifier
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reactivateResponse = await _client.PostAsJsonAsync($"/api/v1/admin/verifiers/{verifierId}/reactivate", new { });
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. Reactivated verifier logs in again
        _client.DefaultRequestHeaders.Authorization = null;
        var reactivatedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = verifierEmail,
            password = "VerifierSecureNewPass2026!"
        });
        reactivatedLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        // 9. Admin soft-deletes verifier
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var deleteResponse = await _client.DeleteAsync($"/api/v1/admin/verifiers/{verifierId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Deleted verifier cannot log in
        _client.DefaultRequestHeaders.Authorization = null;
        var deletedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = verifierEmail,
            password = "VerifierSecureNewPass2026!"
        });
        deletedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
