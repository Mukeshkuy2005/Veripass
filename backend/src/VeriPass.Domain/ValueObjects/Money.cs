namespace VeriPass.Domain.ValueObjects;

public sealed class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "INR")
    {
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "INR" : currency.ToUpperInvariant();
    }

    public override string ToString() => $"{Currency} {Amount:N2}";
}
