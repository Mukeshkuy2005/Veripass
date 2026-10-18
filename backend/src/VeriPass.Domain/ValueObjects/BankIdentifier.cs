using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public static class BankIdentifier
{
    private static readonly Regex IfscRegex = new(@"^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex AccountNumberRegex = new(@"^\d{9,18}$", RegexOptions.Compiled);

    public static bool IsValidIfsc(string? ifsc)
    {
        if (string.IsNullOrWhiteSpace(ifsc)) return false;
        var formatted = ifsc.Trim().ToUpperInvariant();
        return IfscRegex.IsMatch(formatted);
    }

    public static bool IsValidAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return false;
        var formatted = accountNumber.Trim();
        return AccountNumberRegex.IsMatch(formatted);
    }
}
