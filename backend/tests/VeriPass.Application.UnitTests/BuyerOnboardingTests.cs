using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.BuyerOnboarding;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.Application.UnitTests;

public class BuyerOnboardingTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateOnboardingRequest_ShouldSucceed_AndAutoLinkExistingVerifiedCredentials()
    {
        using var context = CreateDbContext();

        var buyerOrg = new Organization { Id = Guid.NewGuid(), Name = "Tata Steel Ltd", Type = OrganizationType.BUYER, Email = "buyer@tatasteel.com" };
        var supplierOrg = new Organization { Id = Guid.NewGuid(), Name = "ABC Industrial Corp", Type = OrganizationType.SUPPLIER, Email = "sales@abc.in" };
        var user = new User { Id = Guid.NewGuid(), Email = "officer@tatasteel.com", FirstName = "Rahul", LastName = "Sharma" };

        var supplier = new Business
        {
            Id = Guid.NewGuid(),
            OrganizationId = supplierOrg.Id,
            BusinessIdNumber = "VP-IN-2026-9999",
            LegalName = "ABC Industrial Corp Private Limited",
            TradeName = "ABC Industrial",
            Gstin = "27AAACB1234F1Z5",
            Pan = "AAACB1234F",
            Cin = "U74999MH2018PTC123456",
            VerificationStatus = VerificationStatus.VERIFIED
        };

        // Bank Account verified via Penny-Drop
        supplier.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = supplier.Id,
            BankName = "HDFC Bank",
            AccountNumberLast4 = "4821",
            VerificationStatus = VerificationStatus.VERIFIED,
            VerifiedAt = DateTime.UtcNow.AddDays(-2)
        });

        context.Organizations.AddRange(buyerOrg, supplierOrg);
        context.Users.Add(user);
        context.Businesses.Add(supplier);

        // Seed Requirements catalogue
        var reqGst = new OnboardingRequirement { Id = Guid.NewGuid(), Code = "GST", Name = "GSTIN", Category = "TAX", IsDefaultMandatory = true };
        var reqBank = new OnboardingRequirement { Id = Guid.NewGuid(), Code = "BANK_ACCOUNT", Name = "Bank", Category = "FINANCIAL", IsDefaultMandatory = true };
        var reqIso = new OnboardingRequirement { Id = Guid.NewGuid(), Code = "ISO_9001", Name = "ISO 9001", Category = "QUALITY", IsDefaultMandatory = false };
        context.OnboardingRequirements.AddRange(reqGst, reqBank, reqIso);
        await context.SaveChangesAsync();

        var handler = new CreateOnboardingRequestCommandHandler(context);
        var command = new CreateOnboardingRequestCommand(
            buyerOrg.Id,
            supplier.Id,
            user.Id,
            "Empanelment for precision fasteners",
            "Precision Tooling",
            "Procurement Division",
            "Direct OEM Vendor",
            "Welcome to Tata Steel vendor network",
            DateTime.UtcNow.AddDays(30),
            new List<RequirementSelectionInput>
            {
                new("GST", true),
                new("BANK_ACCOUNT", true),
                new("ISO_9001", false)
            },
            SendImmediately: true
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = await context.OnboardingRequests
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == result.Value);

        saved.Should().NotBeNull();
        saved!.BuyerOrganizationId.Should().Be(buyerOrg.Id);
        saved.SupplierBusinessId.Should().Be(supplier.Id);
        saved.Requirements.Should().HaveCount(3);

        // Auto-linked verification check
        var gstReq = saved.Requirements.First(r => r.RequirementCode == "GST");
        gstReq.Status.Should().Be(OnboardingRequirementStatus.VERIFIED);
        gstReq.VerificationStatus.Should().Be("VERIFIED");

        var bankReq = saved.Requirements.First(r => r.RequirementCode == "BANK_ACCOUNT");
        bankReq.Status.Should().Be(OnboardingRequirementStatus.VERIFIED);

        var isoReq = saved.Requirements.First(r => r.RequirementCode == "ISO_9001");
        isoReq.Status.Should().Be(OnboardingRequirementStatus.PENDING);

        // Audit log created
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == saved.Id.ToString() && a.Action == "ONBOARDING_CREATED");
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("ONBOARDING_CREATED");
    }

    [Fact]
    public async Task CreateOnboardingRequest_ShouldFail_WhenDuplicateActiveRequestExists()
    {
        using var context = CreateDbContext();

        var buyerOrgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Organizations.Add(new Organization { Id = buyerOrgId, Name = "Buyer Org", Email = "buyer@test.com" });
        context.Businesses.Add(new Business { Id = supplierId, LegalName = "Supplier Ltd", BusinessIdNumber = "VP-1" });

        // Existing active request
        context.OnboardingRequests.Add(new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = buyerOrgId,
            SupplierBusinessId = supplierId,
            CreatedByUserId = userId,
            Status = OnboardingStatus.IN_VERIFICATION,
            Purpose = "Initial Onboarding"
        });
        await context.SaveChangesAsync();

        var handler = new CreateOnboardingRequestCommandHandler(context);
        var command = new CreateOnboardingRequestCommand(
            buyerOrgId,
            supplierId,
            userId,
            "Second Duplicate Request",
            "Category",
            "Dept",
            "Vendor",
            null,
            null,
            new List<RequirementSelectionInput>()
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("active onboarding request already exists");
    }

    [Fact]
    public async Task ApproveOnboardingRequest_ShouldFail_WhenMandatoryRequirementPending()
    {
        using var context = CreateDbContext();

        var buyerOrgId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var buyerOrg = new Organization { Id = buyerOrgId, Name = "Buyer Org", Email = "buyer@test.com" };
        var supplier = new Business { Id = supplierId, LegalName = "Supplier Ltd", BusinessIdNumber = "VP-100" };
        context.Organizations.Add(buyerOrg);
        context.Businesses.Add(supplier);

        var ob = new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = buyerOrgId,
            SupplierBusinessId = supplierId,
            CreatedByUserId = approverId,
            Status = OnboardingStatus.IN_VERIFICATION,
            Purpose = "Testing Approval Validation"
        };

        // Mandatory requirement that is still PENDING
        ob.Requirements.Add(new OnboardingRequestRequirement
        {
            Id = Guid.NewGuid(),
            OnboardingRequestId = ob.Id,
            RequirementId = Guid.NewGuid(),
            RequirementCode = "BANK_ACCOUNT",
            RequirementName = "Bank Penny-Drop",
            IsMandatory = true,
            Status = OnboardingRequirementStatus.PENDING,
            VerificationStatus = "PENDING"
        });

        context.OnboardingRequests.Add(ob);
        await context.SaveChangesAsync();

        var handler = new ApproveOnboardingRequestCommandHandler(context);
        var command = new ApproveOnboardingRequestCommand(ob.Id, buyerOrgId, approverId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Mandatory requirement(s) pending verification");
    }

    [Fact]
    public async Task ApproveOnboardingRequest_ShouldSucceed_WhenMandatoryRequirementsVerified()
    {
        using var context = CreateDbContext();

        var buyerOrgId = Guid.NewGuid();
        var supplierBusinessId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var buyerOrg = new Organization { Id = buyerOrgId, Name = "Buyer Org", Email = "buyer@test.com" };
        var supplier = new Business { Id = supplierBusinessId, LegalName = "Supplier Ltd", BusinessIdNumber = "VP-200" };
        context.Organizations.Add(buyerOrg);
        context.Businesses.Add(supplier);

        var ob = new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = buyerOrgId,
            SupplierBusinessId = supplierBusinessId,
            CreatedByUserId = approverId,
            Status = OnboardingStatus.IN_VERIFICATION,
            Purpose = "Testing Approval Success"
        };

        // Mandatory requirements VERIFIED
        ob.Requirements.Add(new OnboardingRequestRequirement
        {
            Id = Guid.NewGuid(),
            OnboardingRequestId = ob.Id,
            RequirementId = Guid.NewGuid(),
            RequirementCode = "GST",
            RequirementName = "GSTIN",
            IsMandatory = true,
            Status = OnboardingRequirementStatus.VERIFIED,
            VerificationStatus = "VERIFIED"
        });
        ob.Requirements.Add(new OnboardingRequestRequirement
        {
            Id = Guid.NewGuid(),
            OnboardingRequestId = ob.Id,
            RequirementId = Guid.NewGuid(),
            RequirementCode = "PAN",
            RequirementName = "PAN Card",
            IsMandatory = true,
            Status = OnboardingRequirementStatus.VERIFIED,
            VerificationStatus = "VERIFIED"
        });
        // Optional requirement can be pending
        ob.Requirements.Add(new OnboardingRequestRequirement
        {
            Id = Guid.NewGuid(),
            OnboardingRequestId = ob.Id,
            RequirementId = Guid.NewGuid(),
            RequirementCode = "ISO_9001",
            RequirementName = "ISO Certificate",
            IsMandatory = false,
            Status = OnboardingRequirementStatus.PENDING,
            VerificationStatus = "PENDING"
        });

        context.OnboardingRequests.Add(ob);
        await context.SaveChangesAsync();

        var handler = new ApproveOnboardingRequestCommandHandler(context);
        var command = new ApproveOnboardingRequestCommand(
            ob.Id,
            buyerOrgId,
            approverId,
            ErpVendorCode: "SAP-VEND-99001",
            PaymentTerms: "Net 60 Days",
            TargetPlant: "Jamshedpur"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await context.OnboardingRequests.FindAsync(ob.Id);
        updated!.Status.Should().Be(OnboardingStatus.APPROVED);
        updated.ApprovedByUserId.Should().Be(approverId);
        updated.ErpVendorCode.Should().Be("SAP-VEND-99001");
        updated.PaymentTerms.Should().Be("Net 60 Days");

        // BuyerSupplierRelationship created with APPROVED status
        var rel = await context.BuyerSupplierRelationships.FirstOrDefaultAsync(r =>
            r.BuyerOrganizationId == buyerOrgId && r.SupplierBusinessId == supplierBusinessId);
        rel.Should().NotBeNull();
        rel!.Status.Should().Be(RelationshipStatus.APPROVED);

        // Audit Log created
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ONBOARDING_APPROVED" || a.Action == "BUYER_APPROVED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectOnboardingRequest_ShouldPersistReason_AndAudit()
    {
        using var context = CreateDbContext();

        var buyerOrgId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var buyerOrg = new Organization { Id = buyerOrgId, Name = "Buyer Org", Email = "buyer@test.com" };
        var supplier = new Business { Id = supplierId, LegalName = "Supplier Ltd", BusinessIdNumber = "VP-300" };
        context.Organizations.Add(buyerOrg);
        context.Businesses.Add(supplier);

        var ob = new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = buyerOrgId,
            SupplierBusinessId = supplierId,
            CreatedByUserId = officerId,
            Status = OnboardingStatus.IN_VERIFICATION,
            Purpose = "Testing Rejection"
        };
        context.OnboardingRequests.Add(ob);
        await context.SaveChangesAsync();

        var handler = new RejectOnboardingRequestCommandHandler(context);
        var command = new RejectOnboardingRequestCommand(
            ob.Id,
            buyerOrgId,
            officerId,
            "Supplier failed ISO environmental audit standards."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await context.OnboardingRequests.FindAsync(ob.Id);
        updated!.Status.Should().Be(OnboardingStatus.REJECTED);
        updated.RejectionReason.Should().Be("Supplier failed ISO environmental audit standards.");
        updated.RejectedByUserId.Should().Be(officerId);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ONBOARDING_REJECTED" || a.Action == "BUYER_REJECTED");
        audit.Should().NotBeNull();
        audit!.NewValuesJson.Should().Contain("Supplier failed ISO environmental audit standards.");
    }

    [Fact]
    public async Task OrganizationScoping_ShouldDenyAccess_WhenBuyerOrgDoesNotMatch()
    {
        using var context = CreateDbContext();

        var buyerOrgA = Guid.NewGuid();
        var buyerOrgB = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var orgA = new Organization { Id = buyerOrgA, Name = "Buyer A", Email = "a@test.com" };
        var orgB = new Organization { Id = buyerOrgB, Name = "Buyer B", Email = "b@test.com" };
        var supplier = new Business { Id = supplierId, LegalName = "Supplier Ltd", BusinessIdNumber = "VP-400" };
        var user = new User { Id = userId, Email = "officer@test.com" };

        context.Organizations.AddRange(orgA, orgB);
        context.Businesses.Add(supplier);
        context.Users.Add(user);

        var ob = new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = buyerOrgA,
            SupplierBusinessId = supplierId,
            CreatedByUserId = userId,
            Status = OnboardingStatus.IN_VERIFICATION,
            Purpose = "Private Sourcing"
        };
        context.OnboardingRequests.Add(ob);
        await context.SaveChangesAsync();

        var handler = new GetOnboardingDetailQueryHandler(context);
        // Buyer B tries to access Buyer A's onboarding request
        var query = new GetOnboardingDetailQuery(ob.Id, buyerOrgB);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unauthorized access");
    }
}
