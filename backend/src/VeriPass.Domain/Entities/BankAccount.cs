using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class BankAccount : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string AccountHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumberLast4 { get; set; } = string.Empty;
    public string? EncryptedAccountNumber { get; set; }
    public string Ifsc { get; set; } = string.Empty;
    public string AccountType { get; set; } = "CURRENT"; // CURRENT, CASH_CREDIT, OVERDRAFT, SAVINGS
    public bool IsPrimary { get; set; } = false;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.UNVERIFIED;
    public DateTime? VerifiedAt { get; set; }
    public string? PennyDropRef { get; set; }
}
