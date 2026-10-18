using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public sealed class CinNumber
{
    private static readonly Regex CinRegex = new(@"^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$", RegexOptions.Compiled);

    public string Value { get; }
    public bool IsListed => Value.StartsWith('L');
    public string StateCode => Value.Length >= 8 ? Value.Substring(6, 2) : string.Empty;
    public string IncorporationYear => Value.Length >= 12 ? Value.Substring(8, 4) : string.Empty;
    public string CompanyType => Value.Length >= 15 ? Value.Substring(12, 3) : string.Empty;

    public CinNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CIN cannot be empty.", nameof(value));

        var formatted = value.Trim().ToUpperInvariant();
        if (!CinRegex.IsMatch(formatted))
            throw new ArgumentException($"Invalid Indian Corporate Identification Number (CIN): '{value}'.", nameof(value));

        Value = formatted;
    }

    public static bool TryParse(string? value, out CinNumber? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var formatted = value.Trim().ToUpperInvariant();
        if (!CinRegex.IsMatch(formatted)) return false;

        result = new CinNumber(formatted);
        return true;
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is CinNumber other && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
