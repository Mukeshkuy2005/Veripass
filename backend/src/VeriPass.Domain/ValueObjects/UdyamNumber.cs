using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public sealed class UdyamNumber
{
    private static readonly Regex UdyamRegex = new(@"^UDYAM-[A-Z]{2}-\d{2}-\d{7}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }
    public string StateCode => Value.Length >= 8 ? Value.Substring(6, 2) : string.Empty;
    public string DistrictCode => Value.Length >= 11 ? Value.Substring(9, 2) : string.Empty;
    public string RegistrationSerial => Value.Length >= 19 ? Value.Substring(12, 7) : string.Empty;

    public UdyamNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Udyam number cannot be empty.", nameof(value));

        var formatted = value.Trim().ToUpperInvariant();
        if (!UdyamRegex.IsMatch(formatted))
            throw new ArgumentException($"Invalid Udyam registration number format: '{value}'. Expected format 'UDYAM-XX-00-0000000'.", nameof(value));

        Value = formatted;
    }

    public static bool TryParse(string? value, out UdyamNumber? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var formatted = value.Trim().ToUpperInvariant();
        if (!UdyamRegex.IsMatch(formatted)) return false;

        result = new UdyamNumber(formatted);
        return true;
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is UdyamNumber other && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
