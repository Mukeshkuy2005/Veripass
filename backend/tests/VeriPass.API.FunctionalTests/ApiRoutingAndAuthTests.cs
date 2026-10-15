using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace VeriPass.API.FunctionalTests;

public class ApiRoutingAndAuthTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;

    public ApiRoutingAndAuthTests(VeriPassApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/businesses/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/admin/dashboard")]
    [InlineData("/api/v1/verifications/queue")]
    [InlineData("/api/v1/suppliers")]
    public async Task ProtectedEndpoints_WithoutJwtToken_ShouldReturn401Unauthorized(string endpoint)
    {
        var response = await _client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicPassportQrEndpoint_ShouldBeAccessibleWithoutAuth()
    {
        var response = await _client.GetAsync("/api/v1/public/passports/sample-vendor-12345/qr");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/svg+xml");
    }

    [Fact]
    public async Task AuthLogin_WithEmptyPayload_ShouldReturn400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonExistentRoute_ShouldReturn404NotFound()
    {
        var response = await _client.GetAsync("/api/v1/non-existent-endpoint-404");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
