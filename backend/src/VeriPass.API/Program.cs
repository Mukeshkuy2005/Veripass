using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VeriPass.API.Extensions;
using VeriPass.API.Middleware;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configure Logging: use Console and Debug, avoid EventLog issues on Windows
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. Add Clean Architecture Layers, CORS, and Controllers
builder.Services.AddApiServices(builder.Configuration);

// 2. JWT Authentication & Permission Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);

// 3. Swagger / OpenAPI Documentation
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// 4. Database auto-initialization / migration check & Admin bootstrap
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        logger.LogInformation("Verifying PostgreSQL database schema...");
        dbContext.Database.EnsureCreated();
        logger.LogInformation("PostgreSQL schema successfully verified and seeded.");

        // Bootstrap Admin User
        SeedAdminUser(scope.ServiceProvider, app.Configuration, logger);

        // Ensure Buyer Onboarding Schema & Statutory Requirements Catalogue
        EnsureOnboardingSchema(dbContext, logger);

        // Ensure Audit Log Schema & System Roles
        EnsureAuditLogSchema(dbContext, logger);

        // Purge legacy mock seed data (ABC Manufacturing, Tata Steel mock orgs, XYZ Components, etc.)
        PurgeMockSeedData(dbContext, logger);

        // Ensure Verification Cases Schema, Indexes, Verifier User, and Initial Verification Cases
        EnsureVerificationCaseSchema(dbContext, scope.ServiceProvider, logger);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not connect to PostgreSQL on startup. Please ensure PostgreSQL server is running.");
    }
}

// 5. HTTP Request Pipeline & Middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwaggerDocumentation();

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Quick redirects to Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/openapi", () => Results.Redirect("/swagger"));
app.MapGet("/openapi/v1.json", () => Results.Redirect("/swagger/v1/swagger.json"));
app.MapGet("/api", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program
{
    private static void SeedAdminUser(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher>();

            var adminEmail = (configuration["Admin:Email"] ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@veripass.in").Trim().ToLowerInvariant();
            var adminPassword = configuration["Admin:Password"] ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@VeriPass2026!";

            var adminRole = dbContext.Roles.FirstOrDefault(r => r.Name == "ADMIN");
            if (adminRole == null)
            {
                adminRole = new Role
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "ADMIN",
                    Description = "System Administrator with full access",
                    SystemRole = true
                };
                dbContext.Roles.Add(adminRole);
                dbContext.SaveChanges();
            }

            var adminUser = dbContext.Users
                .Include(u => u.Memberships)
                .FirstOrDefault(u => u.Email == adminEmail);

            if (adminUser == null)
            {
                logger.LogInformation("Bootstrapping default Platform Administrator account ({Email})...", adminEmail);

                var adminOrg = dbContext.Organizations.FirstOrDefault(o => o.Name == "VeriPass Platform Administration");
                if (adminOrg == null)
                {
                    adminOrg = new Organization
                    {
                        Name = "VeriPass Platform Administration",
                        Type = OrganizationType.BOTH,
                        Email = adminEmail,
                        IsActive = true
                    };
                    dbContext.Organizations.Add(adminOrg);
                    dbContext.SaveChanges();
                }

                adminUser = new User
                {
                    Email = adminEmail,
                    PasswordHash = passwordHasher.HashPassword(adminPassword),
                    FirstName = "Platform",
                    LastName = "Administrator",
                    Status = "ACTIVE",
                    EmailVerified = true,
                    MustChangePassword = false
                };
                dbContext.Users.Add(adminUser);
                dbContext.SaveChanges();

                var membership = new OrganizationMember
                {
                    OrganizationId = adminOrg.Id,
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    Status = "ACTIVE"
                };
                dbContext.OrganizationMembers.Add(membership);

                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = adminUser.Id,
                    OrganizationId = adminOrg.Id,
                    Action = "ADMIN_BOOTSTRAPPED",
                    ResourceType = "User",
                    ResourceId = adminUser.Id.ToString(),
                    NewValuesJson = $"{{\"email\":\"{adminEmail}\",\"role\":\"ADMIN\"}}"
                });

                dbContext.SaveChanges();
                logger.LogInformation("Platform Administrator account ({Email}) bootstrapped successfully.", adminEmail);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bootstrap Platform Administrator account.");
        }
    }

    private static void EnsureOnboardingSchema(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            logger.LogInformation("Verifying and updating Buyer Onboarding schema and statutory requirements...");
            var ddl = @"
CREATE TABLE IF NOT EXISTS ""onboarding_requirements"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Code"" character varying(50) NOT NULL,
    ""Name"" character varying(150) NOT NULL,
    ""Category"" character varying(50) NOT NULL,
    ""Description"" character varying(500),
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""IsDefaultMandatory"" boolean NOT NULL DEFAULT FALSE,
    ""DisplayOrder"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ""onboarding_requests"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""BuyerOrganizationId"" uuid NOT NULL,
    ""SupplierBusinessId"" uuid NOT NULL,
    ""CreatedByUserId"" uuid NOT NULL,
    ""Status"" integer NOT NULL DEFAULT 0,
    ""Purpose"" character varying(250),
    ""BusinessCategory"" character varying(100),
    ""ProcurementDepartment"" character varying(100),
    ""ExpectedRelationship"" character varying(100),
    ""Message"" character varying(1000),
    ""TargetPlant"" character varying(200),
    ""RejectionReason"" character varying(500),
    ""ActionRequiredReason"" character varying(500),
    ""ApprovedAt"" timestamp with time zone,
    ""ApprovedByUserId"" uuid,
    ""RejectedAt"" timestamp with time zone,
    ""RejectedByUserId"" uuid,
    ""CompletedAt"" timestamp with time zone,
    ""ExpiresAt"" timestamp with time zone,
    ""CreatedBy"" character varying(100),
    ""UpdatedBy"" character varying(100),
    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
    ""DeletedAt"" timestamp with time zone,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ""onboarding_request_requirements"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""OnboardingRequestId"" uuid NOT NULL,
    ""RequirementId"" uuid NOT NULL,
    ""RequirementCode"" character varying(50) NOT NULL,
    ""RequirementName"" character varying(150) NOT NULL,
    ""IsMandatory"" boolean NOT NULL DEFAULT TRUE,
    ""Status"" integer NOT NULL DEFAULT 0,
    ""EvidenceDocumentId"" uuid,
    ""VerificationResultId"" uuid,
    ""VerificationStatus"" character varying(50),
    ""VerificationSource"" character varying(100),
    ""VerifiedValue"" character varying(500),
    ""FailureReason"" character varying(500),
    ""VerifiedAt"" timestamp with time zone
);

CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_BuyerOrganizationId"" ON ""onboarding_requests"" (""BuyerOrganizationId"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_SupplierBusinessId"" ON ""onboarding_requests"" (""SupplierBusinessId"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_Status"" ON ""onboarding_requests"" (""Status"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_CreatedAt"" ON ""onboarding_requests"" (""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_ExpiresAt"" ON ""onboarding_requests"" (""ExpiresAt"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_requests_BuyerOrganizationId_SupplierBusinessId"" ON ""onboarding_requests"" (""BuyerOrganizationId"", ""SupplierBusinessId"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_request_requirements_OnboardingRequestId"" ON ""onboarding_request_requirements"" (""OnboardingRequestId"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_request_requirements_RequirementId"" ON ""onboarding_request_requirements"" (""RequirementId"");
CREATE INDEX IF NOT EXISTS ""IX_onboarding_request_requirements_Status"" ON ""onboarding_request_requirements"" (""Status"");
";
            dbContext.Database.ExecuteSqlRaw(ddl);

            if (!dbContext.OnboardingRequirements.Any())
            {
                var catalogue = new List<OnboardingRequirement>
                {
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0001-0000-0000-000000000001"), Code = "GST", Name = "GSTIN Active Status & Return Filing Proof", Category = "TAX", Description = "Active registration on GSTN with 24-month zero filing default proof for ITC safeguard.", IsActive = true, IsDefaultMandatory = true, DisplayOrder = 1, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0002-0000-0000-000000000002"), Code = "PAN", Name = "Corporate PAN & Legal Identity", Category = "STATUTORY", Description = "Income Tax Department verified PAN card with matching company name.", IsActive = true, IsDefaultMandatory = true, DisplayOrder = 2, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0003-0000-0000-000000000003"), Code = "CIN", Name = "MCA-21 Corporate Registry & Directors (CIN)", Category = "STATUTORY", Description = "Ministry of Corporate Affairs registration, active status and Director DINs.", IsActive = true, IsDefaultMandatory = true, DisplayOrder = 3, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0004-0000-0000-000000000004"), Code = "BANK_ACCOUNT", Name = "NPCI Penny-Drop Bank Account Verification", Category = "FINANCIAL", Description = "Live penny-drop account validation with 100% beneficiary legal name match.", IsActive = true, IsDefaultMandatory = true, DisplayOrder = 4, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0005-0000-0000-000000000005"), Code = "REGISTERED_ADDRESS", Name = "Registered Business Office Address Verification", Category = "STATUTORY", Description = "Electricity consumer ID / municipal registry matched registered office address.", IsActive = true, IsDefaultMandatory = true, DisplayOrder = 5, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0006-0000-0000-000000000006"), Code = "FACTORY_ADDRESS", Name = "Manufacturing Factory / Plant Premises Geo-Inspection", Category = "QUALITY", Description = "Geo-tagged manufacturing premises and factory license verification.", IsActive = true, IsDefaultMandatory = false, DisplayOrder = 6, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0007-0000-0000-000000000007"), Code = "ISO_9001", Name = "ISO 9001:2015 Quality Management Certification", Category = "QUALITY", Description = "IAF/NABCB accredited quality management certificate.", IsActive = true, IsDefaultMandatory = false, DisplayOrder = 7, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0008-0000-0000-000000000008"), Code = "ISO_14001", Name = "ISO 14001 Environmental Management Certification", Category = "SAFETY", Description = "Environmental safety certification and Pollution Control Board consent.", IsActive = true, IsDefaultMandatory = false, DisplayOrder = 8, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0009-0000-0000-000000000009"), Code = "MSME", Name = "Ministry of MSME Udyam Registration", Category = "STATUTORY", Description = "Valid Udyam Certificate for priority sector compliance and Samadhaan terms.", IsActive = true, IsDefaultMandatory = false, DisplayOrder = 9, CreatedAt = DateTime.UtcNow },
                    new OnboardingRequirement { Id = Guid.Parse("bbbbbbbb-0010-0000-0000-000000000010"), Code = "INSURANCE", Name = "Commercial General & Workman Liability Insurance", Category = "SAFETY", Description = "Active general liability policy with third-party indemnification.", IsActive = true, IsDefaultMandatory = false, DisplayOrder = 10, CreatedAt = DateTime.UtcNow }
                };
                dbContext.OnboardingRequirements.AddRange(catalogue);
                dbContext.SaveChanges();
                logger.LogInformation("Statutory Onboarding Requirements catalogue initialized with {Count} definitions.", catalogue.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure buyer onboarding schema.");
        }
    }

    private static void EnsureAuditLogSchema(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            logger.LogInformation("Verifying and updating Audit Log schema and system roles...");
            var ddl = @"
ALTER TABLE ""audit_logs"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(50) DEFAULT 'GENERAL';
ALTER TABLE ""audit_logs"" ADD COLUMN IF NOT EXISTS ""TargetName"" character varying(250);
ALTER TABLE ""audit_logs"" ADD COLUMN IF NOT EXISTS ""Result"" character varying(20) DEFAULT 'SUCCESS';
ALTER TABLE ""audit_logs"" ADD COLUMN IF NOT EXISTS ""EventCode"" character varying(50);
ALTER TABLE ""audit_logs"" ADD COLUMN IF NOT EXISTS ""MetadataJson"" jsonb;

CREATE INDEX IF NOT EXISTS ""IX_audit_logs_OrganizationId_CreatedAt"" ON ""audit_logs"" (""OrganizationId"", ""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_Category"" ON ""audit_logs"" (""Category"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_Action"" ON ""audit_logs"" (""Action"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_Result"" ON ""audit_logs"" (""Result"");
CREATE INDEX IF NOT EXISTS ""IX_audit_logs_EventCode"" ON ""audit_logs"" (""EventCode"");
";
            dbContext.Database.ExecuteSqlRaw(ddl);

            // Ensure system roles exist
            var buyerRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            if (!dbContext.Roles.Any(r => r.Name == "BUYER"))
            {
                dbContext.Roles.Add(new Role
                {
                    Id = buyerRoleId,
                    Name = "BUYER",
                    Description = "Enterprise Buyer Procurement & Compliance Representative",
                    SystemRole = true
                });
            }

            var supplierRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            if (!dbContext.Roles.Any(r => r.Name == "SUPPLIER"))
            {
                dbContext.Roles.Add(new Role
                {
                    Id = supplierRoleId,
                    Name = "SUPPLIER",
                    Description = "Supplier Organization Representative",
                    SystemRole = true
                });
            }

            dbContext.SaveChanges();
            logger.LogInformation("Audit Log schema and system roles verified.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure audit log schema.");
        }
    }

    private static void PurgeMockSeedData(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            logger.LogInformation("Purging legacy mock seed data (mock buyers, mock suppliers, mock requests, mock users)...");
            var purgeSql = @"
DO $$
BEGIN
    -- 1. Onboarding request requirements for mock requests or mock suppliers
    DELETE FROM ""onboarding_request_requirements"" 
    WHERE ""OnboardingRequestId"" IN (
        SELECT ""Id"" FROM ""onboarding_requests"" 
        WHERE ""BuyerOrganizationId""::text LIKE 'b1111111%' 
           OR ""BuyerOrganizationId""::text LIKE '88888888%' 
           OR ""SupplierBusinessId""::text LIKE 'd%'
    );

    -- 2. Mock onboarding requests
    DELETE FROM ""onboarding_requests"" 
    WHERE ""BuyerOrganizationId""::text LIKE 'b1111111%' 
       OR ""BuyerOrganizationId""::text LIKE '88888888%' 
       OR ""SupplierBusinessId""::text LIKE 'd%';

    -- 3. Shortlist items for mock businesses or mock buyer org shortlists
    DELETE FROM ""shortlist_items""
    WHERE ""BusinessId""::text LIKE 'd%'
       OR ""ShortlistId"" IN (
           SELECT ""Id"" FROM ""shortlists"" 
           WHERE ""BuyerOrganizationId""::text LIKE 'b1111111%' 
              OR ""BuyerOrganizationId""::text LIKE '88888888%'
       );

    -- 4. Shortlists for mock buyer orgs
    DELETE FROM ""shortlists""
    WHERE ""BuyerOrganizationId""::text LIKE 'b1111111%' 
       OR ""BuyerOrganizationId""::text LIKE '88888888%';

    -- 5. Verification checks and evidences for mock cases
    DELETE FROM ""verification_checks"" WHERE ""VerificationCaseId"" IN (
        SELECT ""Id"" FROM ""verification_cases"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""verification_evidence"" WHERE ""VerificationCaseId"" IN (
        SELECT ""Id"" FROM ""verification_cases"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""verification_cases"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 6. Verification requests
    DELETE FROM ""verification_requests"" 
    WHERE ""SupplierBusinessId""::text LIKE 'd%' 
       OR ""BuyerOrganizationId""::text LIKE 'b1111111%' 
       OR ""BuyerOrganizationId""::text LIKE '88888888%';

    -- 7. Invitations
    DELETE FROM ""invitations""
    WHERE ""SupplierBusinessId""::text LIKE 'd%'
       OR ""BuyerOrganizationId""::text LIKE 'b1111111%'
       OR ""BuyerOrganizationId""::text LIKE '88888888%';

    -- 8. Buyer supplier relationships
    DELETE FROM ""buyer_supplier_relationships"" 
    WHERE ""SupplierBusinessId""::text LIKE 'd%' 
       OR ""BuyerOrganizationId""::text LIKE 'b1111111%' 
       OR ""BuyerOrganizationId""::text LIKE '88888888%';

    -- 9. Passport access logs & shares
    DELETE FROM ""passport_access_logs"" WHERE ""PassportId"" IN (
        SELECT ""Id"" FROM ""business_passports"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""passport_shares"" WHERE ""PassportId"" IN (
        SELECT ""Id"" FROM ""business_passports"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""business_passports"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 10. Trust scores & snapshots
    DELETE FROM ""trust_score_snapshots"" WHERE ""BusinessId""::text LIKE 'd%';
    DELETE FROM ""trust_scores"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 11. Risk signals
    DELETE FROM ""risk_signals"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 12. Compliance events
    DELETE FROM ""compliance_events"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 13. Disputes & dispute messages
    DELETE FROM ""dispute_messages"" WHERE ""DisputeId"" IN (
        SELECT ""Id"" FROM ""disputes"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""disputes"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 14. Credentials
    DELETE FROM ""credentials"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 15. Documents and versions
    DELETE FROM ""document_versions"" WHERE ""DocumentId"" IN (
        SELECT ""Id"" FROM ""documents"" WHERE ""BusinessId""::text LIKE 'd%'
    );
    DELETE FROM ""documents"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 16. Business addresses, directors, bank accounts
    DELETE FROM ""business_addresses"" WHERE ""BusinessId""::text LIKE 'd%';
    DELETE FROM ""directors"" WHERE ""BusinessId""::text LIKE 'd%';
    DELETE FROM ""bank_accounts"" WHERE ""BusinessId""::text LIKE 'd%';

    -- 17. Businesses (deleting all mock GUIDs d1111111 through d7777777 and any attached to mock orgs)
    DELETE FROM ""businesses"" 
    WHERE ""Id""::text LIKE 'd%' 
       OR ""OrganizationId""::text LIKE 'b1111111%' 
       OR ""OrganizationId""::text LIKE '88888888%';

    -- 18. Audit logs for mock entities and users
    DELETE FROM ""audit_logs"" 
    WHERE ""OrganizationId""::text LIKE 'b1111111%' 
       OR ""OrganizationId""::text LIKE '88888888%' 
       OR ""UserId""::text LIKE 'a1111111%' 
       OR ""UserId""::text LIKE '99999999%' 
       OR ""UserId""::text LIKE 'c1111111%';

    -- 19. Notifications for mock users
    DELETE FROM ""notifications""
    WHERE ""UserId""::text LIKE 'a1111111%' 
       OR ""UserId""::text LIKE '99999999%' 
       OR ""UserId""::text LIKE 'c1111111%';

    -- 20. Organization members
    DELETE FROM ""organization_members"" 
    WHERE ""OrganizationId""::text LIKE 'b1111111%' 
       OR ""OrganizationId""::text LIKE '88888888%' 
       OR ""UserId""::text LIKE 'a1111111%' 
       OR ""UserId""::text LIKE '99999999%' 
       OR ""UserId""::text LIKE 'c1111111%';

    -- 21. Refresh tokens for mock users
    DELETE FROM ""refresh_tokens"" 
    WHERE ""UserId""::text LIKE 'a1111111%' 
       OR ""UserId""::text LIKE '99999999%' 
       OR ""UserId""::text LIKE 'c1111111%';

    -- 22. Users (except admin@veripass.in and registered users)
    DELETE FROM ""users"" 
    WHERE (""Id""::text LIKE 'a1111111%' OR ""Id""::text LIKE '99999999%' OR ""Id""::text LIKE 'c1111111%'
           OR ""Email"" IN ('buyer@veripass.in', 'supplier@veripass.in', 'priya.singh@tatasteel.com', 
                           'ankit.verma@tatasteel.com', 'neha.gupta@tatasteel.com', 'vikram.aditya@tatasteel.com',
                           'contact@abcmanufacturing.in', 'sales@xyzcomponents.in', 'ops@bharatsemi.in',
                           'info@deltaelectric.in', 'procurement@pqrheavy.in', 'fleet@kavachfreight.in',
                           'defense@shreestructures.in'))
      AND ""Email"" != 'admin@veripass.in'
      AND ""Email"" != 'verifier@veripass.in';

    -- 23. Organizations
    DELETE FROM ""organizations"" 
    WHERE ""Id""::text LIKE 'b1111111%' 
       OR ""Id""::text LIKE '88888888%';
END $$;
";
            dbContext.Database.ExecuteSqlRaw(purgeSql);
            logger.LogInformation("Legacy mock seed data purged successfully. Only genuine registered entities and platform admin remain.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not complete mock seed data purge: {Message}", ex.Message);
        }
    }

    private static void EnsureVerificationCaseSchema(ApplicationDbContext dbContext, IServiceProvider services, ILogger logger)
    {
        try
        {
            logger.LogInformation("Verifying and updating Verification Cases schema and indexes...");
            var ddl = @"
ALTER TABLE ""verification_cases"" ADD COLUMN IF NOT EXISTS ""RiskLevel"" character varying(20) DEFAULT 'LOW';
ALTER TABLE ""verification_cases"" ADD COLUMN IF NOT EXISTS ""EscalatedAt"" timestamp with time zone;

ALTER TABLE ""users"" ADD COLUMN IF NOT EXISTS ""AssignedScopeJson"" text;

CREATE INDEX IF NOT EXISTS ""IX_verification_cases_Status"" ON ""verification_cases"" (""Status"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_Priority"" ON ""verification_cases"" (""Priority"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_RiskLevel"" ON ""verification_cases"" (""RiskLevel"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_AssignedVerifierId"" ON ""verification_cases"" (""AssignedVerifierId"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_CreatedAt"" ON ""verification_cases"" (""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_CompletedAt"" ON ""verification_cases"" (""CompletedAt"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_Status_Priority"" ON ""verification_cases"" (""Status"", ""Priority"");
CREATE INDEX IF NOT EXISTS ""IX_verification_cases_Assigned_Status"" ON ""verification_cases"" (""AssignedVerifierId"", ""Status"");
";
            dbContext.Database.ExecuteSqlRaw(ddl);

            // 1. Ensure dedicated verifier account exists: verifier@veripass.in (Rahul Sharma)
            var passwordHasher = services.GetRequiredService<IPasswordHasher>();
            var verifierRole = dbContext.Roles.FirstOrDefault(r => r.Name == "VERIFIER");
            if (verifierRole == null)
            {
                verifierRole = new Role
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = "VERIFIER",
                    Description = "Verification Operations Agent / Compliance Reviewer",
                    SystemRole = true
                };
                dbContext.Roles.Add(verifierRole);
                dbContext.SaveChanges();
            }

            var opsOrg = dbContext.Organizations.FirstOrDefault(o => o.Name == "VeriPass Compliance Operations");
            if (opsOrg == null)
            {
                opsOrg = new Organization
                {
                    Name = "VeriPass Compliance Operations",
                    Type = OrganizationType.BOTH,
                    Email = "operations@veripass.in",
                    IsActive = true
                };
                dbContext.Organizations.Add(opsOrg);
                dbContext.SaveChanges();
            }

            var verifierUser = dbContext.Users
                .Include(u => u.Memberships)
                .FirstOrDefault(u => u.Email == "verifier@veripass.in");

            var defaultScopeJson = "{\"regions\":[\"Uttar Pradesh\",\"Delhi\",\"Haryana\"],\"categories\":[\"GST\",\"PAN\",\"Business Identity\",\"Address\",\"Certifications\"]}";

            if (verifierUser == null)
            {
                verifierUser = new User
                {
                    Email = "verifier@veripass.in",
                    PasswordHash = passwordHasher.HashPassword("Verifier@VeriPass2026!"),
                    FirstName = "Rahul",
                    LastName = "Sharma",
                    Status = "ACTIVE",
                    EmailVerified = true,
                    MustChangePassword = false,
                    LastLoginAt = DateTime.UtcNow.AddMinutes(-42),
                    AssignedScopeJson = defaultScopeJson
                };
                dbContext.Users.Add(verifierUser);
                dbContext.SaveChanges();

                dbContext.OrganizationMembers.Add(new OrganizationMember
                {
                    OrganizationId = opsOrg.Id,
                    UserId = verifierUser.Id,
                    RoleId = verifierRole.Id,
                    Status = "ACTIVE"
                });
                dbContext.SaveChanges();
                logger.LogInformation("Dedicated platform verifier account (verifier@veripass.in) bootstrapped.");
            }
            else
            {
                bool updated = false;
                if (string.IsNullOrEmpty(verifierUser.AssignedScopeJson))
                {
                    verifierUser.AssignedScopeJson = defaultScopeJson;
                    updated = true;
                }
                if (!verifierUser.LastLoginAt.HasValue)
                {
                    verifierUser.LastLoginAt = DateTime.UtcNow.AddMinutes(-42);
                    updated = true;
                }
                if (updated)
                {
                    dbContext.SaveChanges();
                }
            }

            // 2. Ensure primary businesses exist for compliance workstation operations
            var abcBiz = dbContext.Businesses
                .Include(b => b.Addresses)
                .Include(b => b.Passport)
                .FirstOrDefault(b => b.LegalName == "ABC INDUSTRIAL DYNAMICS");

            if (abcBiz == null)
            {
                abcBiz = new Business
                {
                    OrganizationId = opsOrg.Id,
                    BusinessIdNumber = "VP-IN-2026-92644",
                    LegalName = "ABC INDUSTRIAL DYNAMICS",
                    TradeName = "ABC INDUSTRIAL DYNAMICS",
                    BusinessType = "Private Limited",
                    IncorporationDate = new DateTime(2020, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                    Gstin = "27AABCU9603R1ZM",
                    Pan = "AABCU9603R",
                    Cin = "U28999MH2020PTC345678",
                    VerificationStatus = VerificationStatus.IN_PROGRESS,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Businesses.Add(abcBiz);
                dbContext.SaveChanges();

                dbContext.BusinessAddresses.Add(new BusinessAddress
                {
                    BusinessId = abcBiz.Id,
                    AddressType = "REGISTERED",
                    Line1 = "Plot 42, MIDC Industrial Area, Phase II",
                    City = "Navi Mumbai",
                    State = "Maharashtra",
                    Pincode = "400705",
                    Country = "India",
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                });

                dbContext.BusinessPassports.Add(new BusinessPassport
                {
                    BusinessId = abcBiz.Id,
                    PassportNumber = "VP-IN-2026-92644",
                    PublicIdentifier = "abc-industrial-92644",
                    Status = "ACTIVE",
                    IssuedAt = DateTime.UtcNow.AddDays(-30),
                    CreatedAt = DateTime.UtcNow
                });
                dbContext.SaveChanges();
            }

            var axionisBiz = dbContext.Businesses
                .Include(b => b.Addresses)
                .Include(b => b.Passport)
                .FirstOrDefault(b => b.LegalName == "Axionis manufacturing pvt ltd");

            if (axionisBiz == null)
            {
                axionisBiz = new Business
                {
                    OrganizationId = opsOrg.Id,
                    BusinessIdNumber = "VP-IN-2026-46190",
                    LegalName = "Axionis manufacturing pvt ltd",
                    TradeName = "Axionis Manufacturing",
                    BusinessType = "Private Limited",
                    IncorporationDate = new DateTime(2021, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    Gstin = "29AABCA1234F1Z5",
                    Pan = "AABCA1234F",
                    Cin = "U72900KA2021PTC145678",
                    VerificationStatus = VerificationStatus.IN_PROGRESS,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Businesses.Add(axionisBiz);
                dbContext.SaveChanges();

                dbContext.BusinessAddresses.Add(new BusinessAddress
                {
                    BusinessId = axionisBiz.Id,
                    AddressType = "REGISTERED",
                    Line1 = "104, Electronic City Phase 1",
                    City = "Bengaluru",
                    State = "Karnataka",
                    Pincode = "560100",
                    Country = "India",
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                });

                dbContext.BusinessPassports.Add(new BusinessPassport
                {
                    BusinessId = axionisBiz.Id,
                    PassportNumber = "VP-IN-2026-46190",
                    PublicIdentifier = "axionis-mfg-46190",
                    Status = "ACTIVE",
                    IssuedAt = DateTime.UtcNow.AddDays(-20),
                    CreatedAt = DateTime.UtcNow
                });
                dbContext.SaveChanges();
            }

            // Ensure canonical Active Case VP-18291 is always present in DB
            var canonicalCaseId = Guid.Parse("18291000-0000-0000-0000-000000000001");
            if (!dbContext.VerificationCases.Any(c => c.Id == canonicalCaseId))
            {
                dbContext.VerificationCases.Add(new VerificationCase
                {
                    Id = canonicalCaseId,
                    BusinessId = abcBiz.Id,
                    VerificationType = "GST",
                    Status = VerificationStatus.IN_PROGRESS,
                    Priority = "HIGH",
                    RiskLevel = RiskLevel.LOW,
                    AssignedVerifierId = verifierUser.Id,
                    StartedAt = DateTime.UtcNow.AddMinutes(-30),
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    DecisionReason = "Active verification session for GST registration certificate Form GST REG-06."
                });
                dbContext.SaveChanges();
                logger.LogInformation("Canonical verification case VP-18291 seeded.");
            }

            // 3. Seed initial verification cases for compliance queue if needed
            if (dbContext.VerificationCases.Count() <= 1)
            {
                if (abcBiz != null && axionisBiz != null)
                {
                    logger.LogInformation("Seeding initial operational verification cases for compliance workstation...");
                    var now = DateTime.UtcNow;

                    var cases = new List<VerificationCase>
                    {
                        // Priority 1: High Priority / High Risk Pending Case (Section 11 Spec)
                        new VerificationCase
                        {
                            BusinessId = abcBiz.Id,
                            VerificationType = "GST",
                            Status = VerificationStatus.PENDING,
                            Priority = "HIGH",
                            RiskLevel = RiskLevel.HIGH,
                            CreatedAt = now.AddMinutes(-18),
                            DecisionReason = "GSTIN active status and filing consistency check."
                        },
                        // Priority 2: Medium Priority / Medium Risk Pending Case
                        new VerificationCase
                        {
                            BusinessId = axionisBiz.Id,
                            VerificationType = "Address",
                            Status = VerificationStatus.PENDING,
                            Priority = "MEDIUM",
                            RiskLevel = RiskLevel.MEDIUM,
                            CreatedAt = now.AddMinutes(-42),
                            DecisionReason = "Physical plant and registered address geo-coordinates review."
                        },
                        // Priority 3: Medium Priority / Low Risk Pending Case
                        new VerificationCase
                        {
                            BusinessId = abcBiz.Id,
                            VerificationType = "PAN",
                            Status = VerificationStatus.PENDING,
                            Priority = "MEDIUM",
                            RiskLevel = RiskLevel.LOW,
                            CreatedAt = now.AddHours(-1),
                            DecisionReason = "Corporate PAN verification against CBDT registry."
                        },
                        // In Review Cases (Assigned to verifier)
                        new VerificationCase
                        {
                            BusinessId = abcBiz.Id,
                            VerificationType = "Bank",
                            Status = VerificationStatus.IN_PROGRESS,
                            Priority = "NORMAL",
                            RiskLevel = RiskLevel.LOW,
                            AssignedVerifierId = verifierUser.Id,
                            StartedAt = now.AddMinutes(-25),
                            CreatedAt = now.AddHours(-2)
                        },
                        new VerificationCase
                        {
                            BusinessId = axionisBiz.Id,
                            VerificationType = "CIN",
                            Status = VerificationStatus.IN_PROGRESS,
                            Priority = "NORMAL",
                            RiskLevel = RiskLevel.MEDIUM,
                            AssignedVerifierId = verifierUser.Id,
                            StartedAt = now.AddMinutes(-12),
                            CreatedAt = now.AddHours(-3)
                        },
                        // Escalated Case
                        new VerificationCase
                        {
                            BusinessId = axionisBiz.Id,
                            VerificationType = "MSME",
                            Status = VerificationStatus.FLAGGED,
                            FinalDecision = VerificationDecision.ESCALATED,
                            Priority = "HIGH",
                            RiskLevel = RiskLevel.HIGH,
                            EscalatedAt = now.AddMinutes(-55),
                            AssignedVerifierId = verifierUser.Id,
                            DecisionReason = "Udyam enterprise classification mismatch requires senior review.",
                            CreatedAt = now.AddHours(-4)
                        },
                        // Approved Decisions today (Section 17/26 Spec)
                        new VerificationCase
                        {
                            BusinessId = abcBiz.Id,
                            VerificationType = "GST",
                            Status = VerificationStatus.VERIFIED,
                            FinalDecision = VerificationDecision.APPROVED,
                            Priority = "NORMAL",
                            RiskLevel = RiskLevel.LOW,
                            AssignedVerifierId = verifierUser.Id,
                            StartedAt = now.AddHours(-1).AddMinutes(-8),
                            CompletedAt = now.AddHours(-1),
                            DecisionReason = "GSTIN active in CBIC master database. Incorporation verified.",
                            CreatedAt = now.AddHours(-5)
                        },
                        new VerificationCase
                        {
                            BusinessId = axionisBiz.Id,
                            VerificationType = "Bank",
                            Status = VerificationStatus.VERIFIED,
                            FinalDecision = VerificationDecision.APPROVED,
                            Priority = "NORMAL",
                            RiskLevel = RiskLevel.LOW,
                            AssignedVerifierId = verifierUser.Id,
                            StartedAt = now.AddHours(-2).AddMinutes(-6),
                            CompletedAt = now.AddHours(-2),
                            DecisionReason = "NPCI penny-drop bank account matched beneficiary legal name 100%.",
                            CreatedAt = now.AddHours(-6)
                        },
                        // Rejected Decision today
                        new VerificationCase
                        {
                            BusinessId = axionisBiz.Id,
                            VerificationType = "ISO",
                            Status = VerificationStatus.REJECTED,
                            FinalDecision = VerificationDecision.REJECTED,
                            Priority = "HIGH",
                            RiskLevel = RiskLevel.HIGH,
                            AssignedVerifierId = verifierUser.Id,
                            StartedAt = now.AddHours(-3).AddMinutes(-10),
                            CompletedAt = now.AddHours(-3),
                            DecisionReason = "Expired accreditation certificate submitted.",
                            CreatedAt = now.AddHours(-7)
                        }
                    };

                    dbContext.VerificationCases.AddRange(cases);
                    dbContext.SaveChanges();

                    // Seed corresponding audit logs for the historical decisions
                    foreach (var c in cases.Where(x => x.CompletedAt != null))
                    {
                        dbContext.AuditLogs.Add(new AuditLog
                        {
                            UserId = verifierUser.Id,
                            Action = c.FinalDecision == VerificationDecision.APPROVED ? "VERIFICATION_APPROVED" : "VERIFICATION_REJECTED",
                            Category = "VERIFICATION",
                            ResourceType = "VERIFICATION_CASE",
                            ResourceId = c.Id.ToString(),
                            TargetName = c.BusinessId == abcBiz.Id ? abcBiz.LegalName : axionisBiz.LegalName,
                            Result = "SUCCESS",
                            EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                            NewValuesJson = $"{{\"caseId\":\"{c.Id}\",\"decision\":\"{c.FinalDecision}\",\"reason\":\"{c.DecisionReason}\"}}",
                            CreatedAt = c.CompletedAt!.Value
                        });
                    }

                    if (cases.Any(x => x.FinalDecision == VerificationDecision.ESCALATED))
                    {
                        var esc = cases.First(x => x.FinalDecision == VerificationDecision.ESCALATED);
                        dbContext.AuditLogs.Add(new AuditLog
                        {
                            UserId = verifierUser.Id,
                            Action = "VERIFICATION_ESCALATED",
                            Category = "VERIFICATION",
                            ResourceType = "VERIFICATION_CASE",
                            ResourceId = esc.Id.ToString(),
                            TargetName = axionisBiz.LegalName,
                            Result = "SUCCESS",
                            EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                            NewValuesJson = $"{{\"caseId\":\"{esc.Id}\",\"decision\":\"ESCALATED\",\"reason\":\"{esc.DecisionReason}\"}}",
                            CreatedAt = esc.EscalatedAt ?? now
                        });
                    }

                    dbContext.SaveChanges();
                    logger.LogInformation("Seeded {Count} initial verification cases.", cases.Count);
                }
            }

            // Ensure real compliance escalated cases exist in database for V8 work queue
            if (!dbContext.VerificationCases.Any(x => !x.IsDeleted && (x.FinalDecision == VerificationDecision.ESCALATED || x.Status == VerificationStatus.FLAGGED)))
            {
                var now = DateTime.UtcNow;
                var bizAxionis = dbContext.Businesses.FirstOrDefault(b => b.LegalName.Contains("Axionis") || b.TradeName.Contains("Axionis")) ?? abcBiz;
                var escCase1 = new VerificationCase
                {
                    BusinessId = bizAxionis.Id,
                    VerificationType = "MSME",
                    Status = VerificationStatus.FLAGGED,
                    FinalDecision = VerificationDecision.ESCALATED,
                    Priority = "HIGH",
                    RiskLevel = RiskLevel.HIGH,
                    EscalatedAt = now.AddMinutes(-55),
                    AssignedVerifierId = verifierUser.Id,
                    DecisionReason = "Udyam enterprise classification mismatch requires senior review.",
                    CreatedAt = now.AddHours(-4)
                };
                var escCase2 = new VerificationCase
                {
                    BusinessId = abcBiz.Id,
                    VerificationType = "GST",
                    Status = VerificationStatus.FLAGGED,
                    FinalDecision = VerificationDecision.ESCALATED,
                    Priority = "HIGH",
                    RiskLevel = RiskLevel.HIGH,
                    EscalatedAt = now.AddMinutes(-25),
                    AssignedVerifierId = null,
                    DecisionReason = "Conflicting GST registration details against CBIC sovereign registry.",
                    CreatedAt = now.AddHours(-2)
                };
                dbContext.VerificationCases.AddRange(escCase1, escCase2);
                dbContext.SaveChanges();

                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = verifierUser.Id,
                    Action = "VERIFICATION_ESCALATED",
                    Category = "VERIFICATION",
                    ResourceType = "VERIFICATION_CASE",
                    ResourceId = escCase1.Id.ToString(),
                    TargetName = bizAxionis.LegalName,
                    Result = "SUCCESS",
                    EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                    NewValuesJson = $"{{\"caseId\":\"{escCase1.Id}\",\"decision\":\"ESCALATED\",\"reason\":\"{escCase1.DecisionReason}\"}}",
                    CreatedAt = escCase1.EscalatedAt ?? now
                });
                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = verifierUser.Id,
                    Action = "VERIFICATION_ESCALATED",
                    Category = "VERIFICATION",
                    ResourceType = "VERIFICATION_CASE",
                    ResourceId = escCase2.Id.ToString(),
                    TargetName = abcBiz.LegalName,
                    Result = "SUCCESS",
                    EventCode = $"AUD-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                    NewValuesJson = $"{{\"caseId\":\"{escCase2.Id}\",\"decision\":\"ESCALATED\",\"reason\":\"{escCase2.DecisionReason}\"}}",
                    CreatedAt = escCase2.EscalatedAt ?? now
                });
                dbContext.SaveChanges();
                logger.LogInformation("Seeded 2 real compliance escalated cases.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure verification case schema.");
        }
    }
}
// Hot reload trigger
