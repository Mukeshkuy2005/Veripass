using VeriPass.Domain.Common;

namespace VeriPass.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    
    // Controlled Action: e.g. "ONBOARDING_APPROVED", "PASSPORT_VIEWED", "SUPPLIER_SHORTLISTED", "USER_LOGIN"
    public string Action { get; set; } = string.Empty;
    
    // Controlled Category: SUPPLIER, PASSPORT, VERIFICATION, ONBOARDING, CREDENTIAL, USER, ORGANIZATION, CONFIGURATION, SECURITY
    public string Category { get; set; } = "GENERAL";

    // Target Type: "SUPPLIER", "BUSINESS_PASSPORT", "CREDENTIAL", "VERIFICATION", "ONBOARDING", "USER", "ORGANIZATION"
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    
    // Human-readable Target display name, e.g. "ABC Electronics Pvt Ltd", "Master Preferred Shortlist"
    public string? TargetName { get; set; }

    // Result Outcome: SUCCESS, FAILED, DENIED
    public string Result { get; set; } = "SUCCESS";

    // Human-readable reference code, e.g. "AUD-82F93A"
    public string? EventCode { get; set; }

    // Context & Payloads
    public string? OldValuesJson { get; set; } // JSONB
    public string? NewValuesJson { get; set; } // JSONB
    public string? MetadataJson { get; set; }  // JSONB for security / extended metadata

    // Security context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; } // RequestId / TraceIdentifier

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional Navigation
    public User? User { get; set; }
    public Organization? Organization { get; set; }

    // Helper property aliases
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string TargetType
    {
        get => ResourceType;
        set => ResourceType = value;
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? TargetId
    {
        get => ResourceId;
        set => ResourceId = value;
    }
}
