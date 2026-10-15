using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public sealed class Gstin
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string Value { get; }
    public string StateCode => Value.Length >= 2 ? Value[..2] : string.Empty;
    public string Pan => Value.Length >= 12 ? Value.Substring(2, 10) : string.Empty;

    public Gstin(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("GSTIN cannot be empty.", nameof(value));

        var formatted = value.Trim().ToUpperInvariant();
        if (!GstinRegex.IsMatch(formatted))
            throw new ArgumentException($"Invalid Indian GSTIN format: '{value}'.", nameof(value));

        Value = formatted;
    }

    public static bool TryParse(string? value, out Gstin? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var formatted = value.Trim().ToUpperInvariant();
        if (!GstinRegex.IsMatch(formatted)) return false;

        result = new Gstin(formatted);
        return true;
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is Gstin other && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
