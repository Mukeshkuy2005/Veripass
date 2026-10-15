using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public sealed class PanNumber
{
    private static readonly Regex PanRegex = new(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", RegexOptions.Compiled);

    public string Value { get; }
    public char EntityType => Value.Length >= 4 ? Value[3] : ' ';

    public PanNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PAN cannot be empty.", nameof(value));

        var formatted = value.Trim().ToUpperInvariant();
        if (!PanRegex.IsMatch(formatted))
            throw new ArgumentException($"Invalid Indian PAN format: '{value}'.", nameof(value));

        Value = formatted;
    }

    public static bool TryParse(string? value, out PanNumber? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var formatted = value.Trim().ToUpperInvariant();
        if (!PanRegex.IsMatch(formatted)) return false;

        result = new PanNumber(formatted);
        return true;
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is PanNumber other && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
