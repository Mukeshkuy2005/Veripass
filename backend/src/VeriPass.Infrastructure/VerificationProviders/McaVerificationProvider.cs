using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.VerificationProviders;

public class McaVerificationProvider : IVerificationProvider
{
    public string ProviderName => "MCA";

    public Task<RegistryVerificationResult> VerifyAsync(
        string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object?>
        {
            ["cin"] = identifier.ToUpperInvariant(),
            ["company_name"] = "ABC MANUFACTURING PRIVATE LIMITED",
            ["roc_code"] = "RoC-Pune",
            ["status"] = "ACTIVE",
            ["authorized_capital_inr"] = 10000000,
            ["paid_up_capital_inr"] = 5000000,
            ["active_charges_count"] = 1,
            ["disqualified_directors_count"] = 0
        };

        return Task.FromResult(new RegistryVerificationResult
        {
            IsValid = true,
            Status = VerificationStatus.VERIFIED,
            ConfidenceScore = 97.00m,
            Provider = ProviderName,
            ReferenceNumber = $"MCA-V3-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            Data = data
        });
    }
}
