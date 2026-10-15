using VeriPass.Domain.Common;
using VeriPass.Domain.Enums;

namespace VeriPass.Domain.Entities;

public class Business : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string BusinessIdNumber { get; set; } = string.Empty; // e.g. VP-2026-XXXXX
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string? BusinessType { get; set; } // Private Limited, LLP, Sole Proprietorship, Partnership
    public DateTime? IncorporationDate { get; set; }
    
    // Indian Statutory Identifiers
    public string? Pan { get; set; }
    public string? Gstin { get; set; }
    public string? Cin { get; set; }
    public string? UdyamNumber { get; set; }
    
    public string? Website { get; set; }
    public Guid? IndustryId { get; set; }
    public int? EmployeeCount { get; set; }
    public string? Description { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.UNVERIFIED;

    // Navigation
    public ICollection<BusinessAddress> Addresses { get; set; } = new List<BusinessAddress>();
    public ICollection<Director> Directors { get; set; } = new List<Director>();
    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<VerificationCase> VerificationCases { get; set; } = new List<VerificationCase>();
    public ICollection<RiskSignal> RiskSignals { get; set; } = new List<RiskSignal>();
    public TrustScore? TrustScore { get; set; }
    public ICollection<TrustScoreSnapshot> TrustScoreSnapshots { get; set; } = new List<TrustScoreSnapshot>();
    public ICollection<ComplianceEvent> ComplianceEvents { get; set; } = new List<ComplianceEvent>();
    public BusinessPassport? Passport { get; set; }
    public ICollection<BuyerSupplierRelationship> SupplierRelationships { get; set; } = new List<BuyerSupplierRelationship>();
    public ICollection<VerificationRequest> ReceivedVerificationRequests { get; set; } = new List<VerificationRequest>();
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
    public ICollection<OnboardingRequest> OnboardingRequests { get; set; } = new List<OnboardingRequest>();
}
