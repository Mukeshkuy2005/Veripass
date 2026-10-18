using Microsoft.EntityFrameworkCore;
using VeriPass.Domain.Entities;

namespace VeriPass.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Business> Businesses { get; }
    DbSet<BusinessAddress> BusinessAddresses { get; }
    DbSet<Director> Directors { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<CredentialType> CredentialTypes { get; }
    DbSet<Credential> Credentials { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<VerificationCase> VerificationCases { get; }
    DbSet<VerificationCheck> VerificationChecks { get; }
    DbSet<VerificationEvidence> VerificationEvidences { get; }
    DbSet<RiskSignal> RiskSignals { get; }
    DbSet<VeriPass.Domain.Entities.TrustScore> TrustScores { get; }
    DbSet<TrustScoreSnapshot> TrustScoreSnapshots { get; }
    DbSet<ComplianceRule> ComplianceRules { get; }
    DbSet<ComplianceEvent> ComplianceEvents { get; }
    DbSet<BusinessPassport> BusinessPassports { get; }
    DbSet<PassportShare> PassportShares { get; }
    DbSet<PassportAccessLog> PassportAccessLogs { get; }
    DbSet<BuyerSupplierRelationship> BuyerSupplierRelationships { get; }
    DbSet<SupplierShortlist> SupplierShortlists { get; }
    DbSet<SupplierShortlistItem> SupplierShortlistItems { get; }
    DbSet<VerificationRequest> VerificationRequests { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<DisputeMessage> DisputeMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OnboardingRequest> OnboardingRequests { get; }
    DbSet<OnboardingRequirement> OnboardingRequirements { get; }
    DbSet<OnboardingRequestRequirement> OnboardingRequestRequirements { get; }
    DbSet<BuyerVerificationProfile> BuyerVerificationProfiles { get; }
    DbSet<CredentialRequirement> CredentialRequirements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
