using FluentAssertions;

namespace VeriPass.API.FunctionalTests;

public class SecurityHeadersMiddlewareTests : IClassFixture<VeriPassApiFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersMiddlewareTests(VeriPassApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRequest_ShouldIncludeMandatorySecurityHeaders()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert Security Headers (OWASP & Production Hardening)
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");

        response.Headers.Should().ContainKey("X-XSS-Protection");
        response.Headers.GetValues("X-XSS-Protection").First().Should().Be("1; mode=block");

        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.GetValues("Referrer-Policy").First().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Request_ShouldGenerateOrPreserveCorrelationId()
    {
        // 1. Without client correlation ID -> Server generates one
        var response1 = await _client.GetAsync("/swagger/v1/swagger.json");
        response1.Headers.Should().ContainKey("X-Correlation-ID");
        var generatedId = response1.Headers.GetValues("X-Correlation-ID").First();
        generatedId.Should().NotBeNullOrWhiteSpace();

        // 2. With client correlation ID -> Server preserves and echoes it back
        var customCorrelationId = "test-correlation-xyz-2026";
        var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        var response2 = await _client.SendAsync(request);
        response2.Headers.Should().ContainKey("X-Correlation-ID");
        response2.Headers.GetValues("X-Correlation-ID").First().Should().Be(customCorrelationId);
    }
}
