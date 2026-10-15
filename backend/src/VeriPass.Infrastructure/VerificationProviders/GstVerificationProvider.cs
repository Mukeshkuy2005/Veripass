using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Enums;
using VeriPass.Domain.ValueObjects;

namespace VeriPass.Infrastructure.VerificationProviders;

public class GstVerificationProvider : IVerificationProvider
{
    public string ProviderName => "GSTN";

    public Task<RegistryVerificationResult> VerifyAsync(
        string identifier, Dictionary<string, string>? additionalParams = null, CancellationToken cancellationToken = default)
    {
        if (!Gstin.TryParse(identifier, out var gstin))
        {
            return Task.FromResult(new RegistryVerificationResult
            {
                IsValid = false,
                Status = VerificationStatus.REJECTED,
                ConfidenceScore = 0,
                Provider = ProviderName,
                ReferenceNumber = $"REF-GSTN-{Guid.NewGuid():N}"[..20],
                ErrorMessage = "Invalid GSTIN format according to GSTN specifications."
            });
        }

        var isSuspended = identifier.EndsWith("X", StringComparison.OrdinalIgnoreCase);
        var filingMonths = new List<string> { "Jul 2026", "Jun 2026", "May 2026", "Apr 2026", "Mar 2026", "Feb 2026" };

        var data = new Dictionary<string, object?>
        {
            ["gstin"] = gstin!.Value,
            ["legal_name"] = "ABC MANUFACTURING PRIVATE LIMITED",
            ["trade_name"] = "ABC INDUSTRIAL DYNAMICS",
            ["status"] = isSuspended ? "SUSPENDED" : "ACTIVE",
            ["constitution"] = "Private Limited Company",
            ["taxpayer_type"] = "Regular",
            ["date_of_registration"] = "2018-07-01",
            ["jurisdiction"] = "Ward 4, Range 12, Pune Zone",
            ["gstr1_filing_status"] = "REGULAR",
            ["gstr3b_filing_status"] = isSuspended ? "DEFAULTED_LAST_MONTH" : "CLEAN_6_MONTHS",
            ["einvoice_enabled"] = true,
            ["itc_eligible"] = !isSuspended,
            ["filing_history"] = filingMonths
        };

        return Task.FromResult(new RegistryVerificationResult
        {
            IsValid = !isSuspended,
            Status = isSuspended ? VerificationStatus.FLAGGED : VerificationStatus.VERIFIED,
            ConfidenceScore = isSuspended ? 40.00m : 98.50m,
            Provider = ProviderName,
            ReferenceNumber = $"GSTN-API-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            Data = data,
            ErrorMessage = isSuspended ? "GSTIN registration is currently SUSPENDED by jurisdictional officer." : null
        });
    }
}
