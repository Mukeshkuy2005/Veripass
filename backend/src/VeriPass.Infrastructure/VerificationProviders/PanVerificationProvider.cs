using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Enums;
using VeriPass.Domain.ValueObjects;

namespace VeriPass.Infrastructure.VerificationProviders;

public class PanVerificationProvider : IVerificationProvider
{
    public string ProviderName => "NSDL";

    public Task<RegistryVerificationResult> VerifyAsync(
        string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        if (!PanNumber.TryParse(identifier, out var pan))
        {
            return Task.FromResult(new RegistryVerificationResult
            {
                IsValid = false,
                Status = VerificationStatus.REJECTED,
                ConfidenceScore = 0,
                Provider = ProviderName,
                ReferenceNumber = $"REF-NSDL-{Guid.NewGuid():N}"[..20],
                ErrorMessage = "Invalid Permanent Account Number (PAN) format."
            });
        }

        var data = new Dictionary<string, object?>
        {
            ["pan"] = pan!.Value,
            ["name_on_card"] = "ABC MANUFACTURING PRIVATE LIMITED",
            ["status"] = "VALID_AND_OPERATIVE",
            ["aadhaar_linked"] = true,
            ["is_206ab_specified_person"] = false, // Not a TDS defaulter
            ["category"] = "Company"
        };

        return Task.FromResult(new RegistryVerificationResult
        {
            IsValid = true,
            Status = VerificationStatus.VERIFIED,
            ConfidenceScore = 99.00m,
            Provider = ProviderName,
            ReferenceNumber = $"NSDL-VER-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            Data = data
        });
    }
}
