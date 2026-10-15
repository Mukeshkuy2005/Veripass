using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Infrastructure.Persistence.Interceptors;

namespace VeriPass.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext, IUnitOfWork
{
    private readonly AuditableEntityInterceptor? _auditableEntityInterceptor;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntityInterceptor? auditableEntityInterceptor = null)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessAddress> BusinessAddresses => Set<BusinessAddress>();
    public DbSet<Director> Directors => Set<Director>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<CredentialType> CredentialTypes => Set<CredentialType>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<VerificationCase> VerificationCases => Set<VerificationCase>();
    public DbSet<VerificationCheck> VerificationChecks => Set<VerificationCheck>();
    public DbSet<VerificationEvidence> VerificationEvidences => Set<VerificationEvidence>();
    public DbSet<RiskSignal> RiskSignals => Set<RiskSignal>();
    public DbSet<TrustScore> TrustScores => Set<TrustScore>();
    public DbSet<TrustScoreSnapshot> TrustScoreSnapshots => Set<TrustScoreSnapshot>();
    public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();
    public DbSet<ComplianceEvent> ComplianceEvents => Set<ComplianceEvent>();
    public DbSet<BusinessPassport> BusinessPassports => Set<BusinessPassport>();
    public DbSet<PassportShare> PassportShares => Set<PassportShare>();
    public DbSet<PassportAccessLog> PassportAccessLogs => Set<PassportAccessLog>();
    public DbSet<BuyerSupplierRelationship> BuyerSupplierRelationships => Set<BuyerSupplierRelationship>();
    public DbSet<SupplierShortlist> SupplierShortlists => Set<SupplierShortlist>();
    public DbSet<SupplierShortlistItem> SupplierShortlistItems => Set<SupplierShortlistItem>();
    public DbSet<VerificationRequest> VerificationRequests => Set<VerificationRequest>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OnboardingRequest> OnboardingRequests => Set<OnboardingRequest>();
    public DbSet<OnboardingRequirement> OnboardingRequirements => Set<OnboardingRequirement>();
    public DbSet<OnboardingRequestRequirement> OnboardingRequestRequirements => Set<OnboardingRequestRequirement>();

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction ??= await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_auditableEntityInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        }
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        // Seed default roles
        var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var supplierRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var buyerRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var verifierRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = adminRoleId, Name = "ADMIN", Description = "System Administrator with full access", SystemRole = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = supplierRoleId, Name = "SUPPLIER", Description = "Supplier Organization Representative", SystemRole = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = buyerRoleId, Name = "BUYER", Description = "Enterprise Buyer / Procurement Officer", SystemRole = true, CreatedAt = DateTime.UtcNow },
            new Role { Id = verifierRoleId, Name = "VERIFIER", Description = "Verification Operations Agent / Compliance Reviewer", SystemRole = true, CreatedAt = DateTime.UtcNow }
        );

        // Seed Indian Credential Types
        modelBuilder.Entity<CredentialType>().HasData(
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"), Code = "GST", Name = "Goods and Services Tax (GSTIN)", Category = "TAX", IsRequired = true, RequiresExpiry = false, VerificationProvider = "GSTN", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0002-0000-0000-000000000002"), Code = "PAN", Name = "Permanent Account Number (PAN)", Category = "STATUTORY", IsRequired = true, RequiresExpiry = false, VerificationProvider = "NSDL", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0003-0000-0000-000000000003"), Code = "CIN", Name = "Corporate Identification Number (CIN)", Category = "STATUTORY", IsRequired = false, RequiresExpiry = false, VerificationProvider = "MCA", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0004-0000-0000-000000000004"), Code = "UDYAM", Name = "MSME Udyam Registration", Category = "STATUTORY", IsRequired = false, RequiresExpiry = false, VerificationProvider = "UDYAM", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0005-0000-0000-000000000005"), Code = "BANK_ACCOUNT", Name = "Bank Account / Cancelled Cheque", Category = "FINANCIAL", IsRequired = true, RequiresExpiry = false, VerificationProvider = "NPCI_PENNY_DROP", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0006-0000-0000-000000000006"), Code = "ISO_9001", Name = "ISO 9001:2015 Quality Management", Category = "QUALITY", IsRequired = false, RequiresExpiry = true, VerificationProvider = "IAF_CERTSEARCH", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0007-0000-0000-000000000007"), Code = "FSSAI", Name = "FSSAI Food Safety License", Category = "SAFETY", IsRequired = false, RequiresExpiry = true, VerificationProvider = "FOSCOS", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0008-0000-0000-000000000008"), Code = "IEC", Name = "Import Export Code (IEC)", Category = "TRADE", IsRequired = false, RequiresExpiry = false, VerificationProvider = "DGFT", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0009-0000-0000-000000000009"), Code = "TRADE_LICENSE", Name = "Municipal Trade License", Category = "STATUTORY", IsRequired = false, RequiresExpiry = true, VerificationProvider = "MUNICIPAL", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CredentialType { Id = Guid.Parse("aaaaaaaa-0010-0000-0000-000000000010"), Code = "INSURANCE", Name = "Commercial & Workman Liability Insurance", Category = "SAFETY", IsRequired = false, RequiresExpiry = true, VerificationProvider = "IIB_INSURANCE", IsActive = true, CreatedAt = DateTime.UtcNow }
        );

        // Seed Onboarding Requirement Catalogue
        modelBuilder.Entity<OnboardingRequirement>().HasData(
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0001-0000-0000-000000000001"), Code = "GST", Name = "GSTIN Active Status & Return Filing Proof", Category = "TAX", Description = "Active registration on GSTN with 24-month zero filing default proof for ITC safeguard.", IsActive = true, IsDefaultMandatory = true, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0002-0000-0000-000000000002"), Code = "PAN", Name = "Corporate PAN & Legal Identity", Category = "STATUTORY", Description = "Income Tax Department verified PAN card with matching company name.", IsActive = true, IsDefaultMandatory = true, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0003-0000-0000-000000000003"), Code = "CIN", Name = "MCA-21 Corporate Registry & Directors (CIN)", Category = "STATUTORY", Description = "Ministry of Corporate Affairs registration, active status and Director DINs.", IsActive = true, IsDefaultMandatory = true, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0004-0000-0000-000000000004"), Code = "BANK_ACCOUNT", Name = "NPCI Penny-Drop Bank Account Verification", Category = "FINANCIAL", Description = "Live penny-drop account validation with 100% beneficiary legal name match.", IsActive = true, IsDefaultMandatory = true, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0005-0000-0000-000000000005"), Code = "REGISTERED_ADDRESS", Name = "Registered Business Office Address Verification", Category = "STATUTORY", Description = "Electricity consumer ID / municipal registry matched registered office address.", IsActive = true, IsDefaultMandatory = true, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0006-0000-0000-000000000006"), Code = "FACTORY_ADDRESS", Name = "Manufacturing Factory / Plant Premises Geo-Inspection", Category = "QUALITY", Description = "Geo-tagged manufacturing premises and factory license verification.", IsActive = true, IsDefaultMandatory = false, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0007-0000-0000-000000000007"), Code = "ISO_9001", Name = "ISO 9001:2015 Quality Management Certification", Category = "QUALITY", Description = "IAF/NABCB accredited quality management certificate.", IsActive = true, IsDefaultMandatory = false, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0008-0000-0000-000000000008"), Code = "ISO_14001", Name = "ISO 14001 Environmental Management Certification", Category = "SAFETY", Description = "Environmental safety certification and Pollution Control Board consent.", IsActive = true, IsDefaultMandatory = false, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0009-0000-0000-000000000009"), Code = "MSME", Name = "Ministry of MSME Udyam Registration", Category = "STATUTORY", Description = "Valid Udyam Certificate for priority sector compliance and Samadhaan terms.", IsActive = true, IsDefaultMandatory = false, CreatedAt = DateTime.UtcNow },
            new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0010-0000-0000-000000000010"), Code = "INSURANCE", Name = "Commercial General & Workman Liability Insurance", Category = "SAFETY", Description = "Active general liability policy with third-party indemnification.", IsActive = true, IsDefaultMandatory = false, CreatedAt = DateTime.UtcNow }
        );
    }
}
