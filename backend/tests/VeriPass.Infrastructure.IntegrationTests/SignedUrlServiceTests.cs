using FluentAssertions;
using Microsoft.Extensions.Configuration;
using VeriPass.Infrastructure.Storage;

namespace VeriPass.Infrastructure.IntegrationTests;

public class SignedUrlServiceTests
{
    private readonly SignedUrlService _service;

    public SignedUrlServiceTests()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Storage:SigningSecret"] = "INTEGRATION_TEST_SECRET_KEY_SHA256_2026"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        _service = new SignedUrlService(config);
    }

    [Fact]
    public void GenerateSignedUrl_ShouldProduceExpectedFormat_WithExpiresAndSignature()
    {
        var storageKey = "businesses/b1/documents/gst_cert.pdf";
        var expiry = TimeSpan.FromMinutes(15);

        var signedUrl = _service.GenerateSignedUrl(storageKey, expiry);

        signedUrl.Should().StartWith("/api/v1/documents/download/businesses/b1/documents/gst_cert.pdf?");
        signedUrl.Should().Contain("expires=");
        signedUrl.Should().Contain("sig=");
    }

    [Fact]
    public void ValidateSignedUrl_WithValidSignature_ShouldReturnTrue()
    {
        var storageKey = "documents/tax/pan_card.pdf";
        var expiry = TimeSpan.FromMinutes(30);

        var url = _service.GenerateSignedUrl(storageKey, expiry);

        // Parse query params
        var uri = new Uri("http://localhost" + url);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expires = long.Parse(queryParams["expires"]!);
        var sig = queryParams["sig"]!;

        var isValid = _service.ValidateSignedUrl(storageKey, expires, sig);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSignedUrl_WhenExpired_ShouldReturnFalse()
    {
        var storageKey = "documents/tax/pan_card.pdf";
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var fakeSig = "abcdef1234567890abcdef1234567890";

        var isValid = _service.ValidateSignedUrl(storageKey, expiredTimestamp, fakeSig);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateSignedUrl_WhenTampered_ShouldReturnFalse()
    {
        var storageKey = "documents/tax/pan_card.pdf";
        var expiry = TimeSpan.FromMinutes(10);
        var url = _service.GenerateSignedUrl(storageKey, expiry);

        var uri = new Uri("http://localhost" + url);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expires = long.Parse(queryParams["expires"]!);
        var sig = queryParams["sig"]!;

        // Attempting to use the signature for a different resource (tampering)
        var tamperedKey = "documents/other_company/confidential.pdf";
        var isValid = _service.ValidateSignedUrl(tamperedKey, expires, sig);

        isValid.Should().BeFalse();
    }
}
