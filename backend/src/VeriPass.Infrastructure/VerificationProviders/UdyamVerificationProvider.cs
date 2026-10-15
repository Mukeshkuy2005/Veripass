using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.VerificationProviders;

public class UdyamVerificationProvider : IVerificationProvider
{
    public string ProviderName => "UDYAM";

    public Task<RegistryVerificationResult> VerifyAsync(
        string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object?>
        {
            ["udyam_number"] = identifier.ToUpperInvariant(),
            ["enterprise_name"] = "ABC MANUFACTURING PRIVATE LIMITED",
            ["enterprise_type"] = "MEDIUM",
            ["major_activity"] = "MANUFACTURING",
            ["status"] = "VERIFIED_ACTIVE",
            ["msmed_act_section15_protected"] = true
        };

        return Task.FromResult(new RegistryVerificationResult
        {
            IsValid = true,
            Status = VerificationStatus.VERIFIED,
            ConfidenceScore = 95.00m,
            Provider = ProviderName,
            ReferenceNumber = $"UDYAM-REG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            Data = data
        });
    }
}
