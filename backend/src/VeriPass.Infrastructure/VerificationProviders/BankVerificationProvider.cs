using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Enums;

namespace VeriPass.Infrastructure.VerificationProviders;

public class BankVerificationProvider : IVerificationProvider
{
    public string ProviderName => "NPCI_PENNY_DROP";

    public Task<RegistryVerificationResult> VerifyAsync(
        string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        var ifsc = additionalParams != null && additionalParams.TryGetValue("ifsc", out var ifscVal) ? ifscVal : "HDFC0000123";
        var accountHolder = additionalParams != null && additionalParams.TryGetValue("name", out var nameVal) ? nameVal : "ABC MANUFACTURING PRIVATE LIMITED";

        var data = new Dictionary<string, object?>
        {
            ["account_number_masked"] = $"XXXXXX{identifier.TakeLast(4).ToArray()}",
            ["registered_beneficiary_name"] = accountHolder,
            ["name_match_score"] = 98.40m,
            ["ifsc_valid"] = true,
            ["bank_name"] = "HDFC BANK",
            ["penny_drop_status"] = "SUCCESS"
        };

        return Task.FromResult(new RegistryVerificationResult
        {
            IsValid = true,
            Status = VerificationStatus.VERIFIED,
            ConfidenceScore = 98.40m,
            Provider = ProviderName,
            ReferenceNumber = $"NPCI-UTR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            Data = data
        });
    }
}
