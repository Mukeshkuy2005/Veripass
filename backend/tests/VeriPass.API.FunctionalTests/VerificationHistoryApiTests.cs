using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VeriPass.Application.Auth;
using VeriPass.Application.Verification;
using Xunit;

namespace VeriPass.API.FunctionalTests;

public class VerificationHistoryApiTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public VerificationHistoryApiTests(VeriPassApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@veripass.in",
            password = "AdminPassword123!"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        auth.Should().NotBeNull();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task GetVerificationHistory_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/verifier/history");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVerificationHistory_WithAdminOrVerifierToken_ReturnsOkWithPaging()
    {
        var token = await GetAdminTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifier/history?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync<VerifierHistoryResponseDto>(_jsonOptions);
        data.Should().NotBeNull();
        data!.Page.Should().Be(1);
        data.PageSize.Should().Be(10);
        data.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVerificationHistoryFilters_ReturnsOkWithFilterOptions()
    {
        var token = await GetAdminTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifier/history/filters");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var filters = await response.Content.ReadFromJsonAsync<VerifierHistoryFilterOptionsDto>(_jsonOptions);
        filters.Should().NotBeNull();
        filters!.Decisions.Should().NotBeEmpty();
        filters.VerificationTypes.Should().NotBeEmpty();
        filters.DateRanges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetVerificationHistoryDetail_WithValidEventOrCase_ReturnsOk()
    {
        var token = await GetAdminTokenAsync();

        // 1. First get history list to find an existing event/case ID
        using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/verifier/history?page=1&pageSize=5");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listRes = await _client.SendAsync(listReq);
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var listData = await listRes.Content.ReadFromJsonAsync<VerifierHistoryResponseDto>(_jsonOptions);
        listData.Should().NotBeNull();

        if (listData!.Items.Count > 0)
        {
            var firstItem = listData.Items[0];
            var eventId = firstItem.EventId;

            using var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/verifier/history/{eventId}");
            detailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var detailRes = await _client.SendAsync(detailReq);
            detailRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var detail = await detailRes.Content.ReadFromJsonAsync<VerifierHistoryDetailDto>(_jsonOptions);
            detail.Should().NotBeNull();
            detail!.CaseId.Should().NotBeNullOrEmpty();
            detail.EvidenceExamined.Should().NotBeEmpty();
        }
    }
}
