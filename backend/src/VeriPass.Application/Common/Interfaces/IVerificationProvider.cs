using VeriPass.Domain.Enums;

namespace VeriPass.Application.Common.Interfaces;

public class RegistryVerificationResult
{
    public bool IsValid { get; set; }
    public VerificationStatus Status { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public Dictionary<string, object?> Data { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public interface IVerificationProviderFactory
{
    IVerificationProvider GetProvider(string providerName);
}

public interface IVerificationProvider
{
    string ProviderName { get; }
    Task<RegistryVerificationResult> VerifyAsync(string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default);
}
