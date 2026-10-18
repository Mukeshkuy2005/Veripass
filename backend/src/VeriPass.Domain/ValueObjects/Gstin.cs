using System.Text.RegularExpressions;

namespace VeriPass.Domain.ValueObjects;

public sealed class Gstin
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string Value { get; }
    public string StateCode => Value.Length >= 2 ? Value[..2] : string.Empty;
    public string Pan => Value.Length >= 12 ? Value.Substring(2, 10) : string.Empty;
    public string EmbeddedPan => Pan;

    public bool IsValidStateCode
    {
        get
        {
            if (int.TryParse(StateCode, out var code))
            {
                return (code >= 1 && code <= 38) || code == 97 || code == 99;
            }
            return false;
        }
    }

    public bool IsValidChecksum => VerifyChecksum(Value, out _);

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

    /// <summary>
    /// Computes and verifies the 15th character checksum according to the official GSTN Mod-36 specification.
    /// </summary>
    public static bool VerifyChecksum(string? gstin, out char expectedChecksumChar)
    {
        expectedChecksumChar = ' ';
        if (string.IsNullOrWhiteSpace(gstin) || gstin.Trim().Length != 15) return false;

        var upper = gstin.Trim().ToUpperInvariant();
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        int sum = 0;
        for (int i = 0; i < 14; i++)
        {
            int val = alphabet.IndexOf(upper[i]);
            if (val < 0) return false;

            int factor = (i % 2 == 0) ? 1 : 2;
            int product = val * factor;
            sum += (product / 36) + (product % 36);
        }

        int remainder = sum % 36;
        int checkCode = (36 - remainder) % 36;
        expectedChecksumChar = alphabet[checkCode];

        return upper[14] == expectedChecksumChar;
    }

    public override string ToString() => Value;
    public override bool Equals(object? obj) => obj is Gstin other && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
