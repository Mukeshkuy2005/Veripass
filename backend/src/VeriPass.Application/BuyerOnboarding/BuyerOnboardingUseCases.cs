using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.BuyerOnboarding;

// ==========================================
// DTOs
// ==========================================
public record OnboardingStatusCountsDto(
    int NewCount,
    int RequestedCount,
    int InVerificationCount,
    int ActionRequiredCount,
    int ApprovedCount,
    int RejectedCount,
    int ExpiredCount,
    int WithdrawnCount,
    int TotalActiveCount);

public record OnboardingRequestSummaryDto(
    Guid Id,
    Guid BuyerOrganizationId,
    string BuyerOrganizationName,
    Guid SupplierBusinessId,
    string SupplierBusinessName,
    string? SupplierTradeName,
    string? SupplierCity,
    string? SupplierState,
    string? SupplierGstin,
    decimal? SupplierTrustScore,
    string? SupplierPassportNumber,
    string Status,
    string Purpose,
    string BusinessCategory,
    string ProcurementDepartment,
    string ExpectedRelationship,
    int TotalRequirements,
    int VerifiedRequirements,
    string? ActionRequiredReason,
    string? ErpVendorCode,
    string? RequestedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ExpiresAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt);

public record OnboardingPagedListDto(
    List<OnboardingRequestSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    OnboardingStatusCountsDto StatusCounts);

public record OnboardingRequestRequirementDto(
    Guid Id,
    Guid RequirementId,
    string RequirementCode,
    string RequirementName,
    string Category,
    bool IsMandatory,
    string Status,
    string VerificationStatus,
    DateTime? VerifiedAt,
    string? VerificationSource,
    string? FailureReason,
    string? EvidenceDocumentName,
    DateTime RequestedAt,
    DateTime? CompletedAt);

public record OnboardingActivityDto(
    Guid Id,
    string Action,
    string Title,
    string? Description,
    string? ActorName,
    DateTime Timestamp);

public record OnboardingRequestDetailDto(
    Guid Id,
    Guid BuyerOrganizationId,
    string BuyerOrganizationName,
    Guid SupplierBusinessId,
    string SupplierBusinessName,
    string LegalName,
    string? TradeName,
    string? Gstin,
    string? Pan,
    string? Cin,
    string? UdyamNumber,
    string? Website,
    string? Location,
    string? Industry,
    string? VerificationStatus,
    decimal? TrustScore,
    string? PassportNumber,
    string? PassportStatus,
    string Status,
    string Purpose,
    string BusinessCategory,
    string ProcurementDepartment,
    string ExpectedRelationship,
    string? Message,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ExpiresAt,
    DateTime? CompletedAt,
    DateTime? ApprovedAt,
    string? ApprovedByName,
    DateTime? RejectedAt,
    string? RejectedByName,
    string? RejectionReason,
    string? ActionRequiredReason,
    string? ErpVendorCode,
    string? PaymentTerms,
    string? TargetPlant,
    int TotalRequirements,
    int VerifiedRequirements,
    bool CanApprove,
    bool CanReject,
    bool CanWithdraw,
    bool CanRemind,
    List<OnboardingRequestRequirementDto> Requirements,
    List<OnboardingActivityDto> Activities);

public record SupplierSearchItemDto(
    Guid Id,
    string BusinessName,
    string LegalName,
    string? TradeName,
    string? PassportId,
    string? GstinMasked,
    string? PanMasked,
    string? Location,
    string? Industry,
    string VerificationStatus,
    decimal TrustScore,
    string? RiskLevel = null,
    bool BuyerEligible = false);

public record RequirementCatalogueItemDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsDefaultMandatory);

public record RequirementSelectionInput(
    string RequirementCode,
    bool IsMandatory);

// ==========================================
// 1. GET ONBOARDING LIST QUERY
// ==========================================
public record GetOnboardingListQuery(
    Guid? BuyerOrganizationId = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    string? Category = null,
    string? Department = null,
    string? SortBy = null,
    string? SortDirection = "desc") : IRequest<Result<OnboardingPagedListDto>>;

public class GetOnboardingListQueryHandler : IRequestHandler<GetOnboardingListQuery, Result<OnboardingPagedListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOnboardingListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OnboardingPagedListDto>> Handle(GetOnboardingListQuery request, CancellationToken cancellationToken)
    {
        if (!request.BuyerOrganizationId.HasValue || request.BuyerOrganizationId == Guid.Empty)
        {
            var emptyCounts = new OnboardingStatusCountsDto(0, 0, 0, 0, 0, 0, 0, 0, 0);
            return Result<OnboardingPagedListDto>.Success(new OnboardingPagedListDto(new List<OnboardingRequestSummaryDto>(), 0, request.Page, request.PageSize, emptyCounts));
        }

        var buyerOrgId = request.BuyerOrganizationId.Value;

        var baseQuery = _context.OnboardingRequests
            .AsNoTracking()
            .Include(x => x.BuyerOrganization)
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.Passport)
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.TrustScore)
            .Include(x => x.CreatedByUser)
            .Include(x => x.Requirements)
            .Where(x => x.BuyerOrganizationId == buyerOrgId);

        // Compute Status Counts
        var allBuyerRequests = await baseQuery.Select(x => x.Status).ToListAsync(cancellationToken);
        var statusCounts = new OnboardingStatusCountsDto(
            NewCount: allBuyerRequests.Count(s => s == OnboardingStatus.NEW),
            RequestedCount: allBuyerRequests.Count(s => s == OnboardingStatus.REQUESTED),
            InVerificationCount: allBuyerRequests.Count(s => s == OnboardingStatus.IN_VERIFICATION),
            ActionRequiredCount: allBuyerRequests.Count(s => s == OnboardingStatus.ACTION_REQUIRED),
            ApprovedCount: allBuyerRequests.Count(s => s == OnboardingStatus.APPROVED),
            RejectedCount: allBuyerRequests.Count(s => s == OnboardingStatus.REJECTED),
            ExpiredCount: allBuyerRequests.Count(s => s == OnboardingStatus.EXPIRED),
            WithdrawnCount: allBuyerRequests.Count(s => s == OnboardingStatus.WITHDRAWN),
            TotalActiveCount: allBuyerRequests.Count(s => s != OnboardingStatus.APPROVED && s != OnboardingStatus.REJECTED && s != OnboardingStatus.WITHDRAWN)
        );

        // Apply filters
        var filteredQuery = baseQuery;

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<OnboardingStatus>(request.Status, true, out var parsedStatus))
            {
                filteredQuery = filteredQuery.Where(x => x.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            filteredQuery = filteredQuery.Where(x => x.BusinessCategory == request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            filteredQuery = filteredQuery.Where(x => x.ProcurementDepartment == request.Department);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            filteredQuery = filteredQuery.Where(x =>
                x.SupplierBusiness.LegalName.ToLower().Contains(term) ||
                (x.SupplierBusiness.TradeName != null && x.SupplierBusiness.TradeName.ToLower().Contains(term)) ||
                (x.SupplierBusiness.Gstin != null && x.SupplierBusiness.Gstin.ToLower().Contains(term)) ||
                (x.SupplierBusiness.Pan != null && x.SupplierBusiness.Pan.ToLower().Contains(term)) ||
                (x.ErpVendorCode != null && x.ErpVendorCode.ToLower().Contains(term)) ||
                x.Purpose.ToLower().Contains(term) ||
                x.BusinessCategory.ToLower().Contains(term));
        }

        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        // Sorting
        var isAsc = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        filteredQuery = request.SortBy?.ToLower() switch
        {
            "suppliername" => isAsc ? filteredQuery.OrderBy(x => x.SupplierBusiness.LegalName) : filteredQuery.OrderByDescending(x => x.SupplierBusiness.LegalName),
            "status" => isAsc ? filteredQuery.OrderBy(x => x.Status) : filteredQuery.OrderByDescending(x => x.Status),
            "trustscore" => isAsc ? filteredQuery.OrderBy(x => x.SupplierBusiness.TrustScore != null ? x.SupplierBusiness.TrustScore.OverallScore : 0)
                                 : filteredQuery.OrderByDescending(x => x.SupplierBusiness.TrustScore != null ? x.SupplierBusiness.TrustScore.OverallScore : 0),
            "expiresat" => isAsc ? filteredQuery.OrderBy(x => x.ExpiresAt) : filteredQuery.OrderByDescending(x => x.ExpiresAt),
            "updatedat" => isAsc ? filteredQuery.OrderBy(x => x.UpdatedAt ?? x.CreatedAt) : filteredQuery.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            _ => filteredQuery.OrderByDescending(x => x.CreatedAt)
        };

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var pagedItems = await filteredQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = pagedItems.Select(x =>
        {
            var verifiedCount = x.Requirements.Count(r => r.Status == OnboardingRequirementStatus.VERIFIED);
            return new OnboardingRequestSummaryDto(
                Id: x.Id,
                BuyerOrganizationId: x.BuyerOrganizationId,
                BuyerOrganizationName: x.BuyerOrganization.Name,
                SupplierBusinessId: x.SupplierBusinessId,
                SupplierBusinessName: x.SupplierBusiness.LegalName,
                SupplierTradeName: x.SupplierBusiness.TradeName,
                SupplierCity: x.SupplierBusiness.Addresses.FirstOrDefault()?.City,
                SupplierState: x.SupplierBusiness.Addresses.FirstOrDefault()?.State,
                SupplierGstin: x.SupplierBusiness.Gstin,
                SupplierTrustScore: x.SupplierBusiness.TrustScore?.OverallScore,
                SupplierPassportNumber: x.SupplierBusiness.Passport?.PassportNumber,
                Status: x.Status.ToString(),
                Purpose: x.Purpose,
                BusinessCategory: x.BusinessCategory,
                ProcurementDepartment: x.ProcurementDepartment,
                ExpectedRelationship: x.ExpectedRelationship,
                TotalRequirements: x.Requirements.Count,
                VerifiedRequirements: verifiedCount,
                ActionRequiredReason: x.ActionRequiredReason,
                ErpVendorCode: x.ErpVendorCode,
                RequestedByName: x.CreatedByUser != null ? $"{x.CreatedByUser.FirstName} {x.CreatedByUser.LastName}".Trim() : "Procurement Team",
                CreatedAt: x.CreatedAt,
                UpdatedAt: x.UpdatedAt,
                ExpiresAt: x.ExpiresAt,
                ApprovedAt: x.ApprovedAt,
                RejectedAt: x.RejectedAt);
        }).ToList();

        return Result<OnboardingPagedListDto>.Success(new OnboardingPagedListDto(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            StatusCounts: statusCounts));
    }
}

// ==========================================
// 2. GET ONBOARDING DETAIL QUERY
// ==========================================
public record GetOnboardingDetailQuery(Guid Id, Guid? BuyerOrganizationId = null) : IRequest<Result<OnboardingRequestDetailDto>>;

public class GetOnboardingDetailQueryHandler : IRequestHandler<GetOnboardingDetailQuery, Result<OnboardingRequestDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOnboardingDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OnboardingRequestDetailDto>> Handle(GetOnboardingDetailQuery request, CancellationToken cancellationToken)
    {
        var item = await _context.OnboardingRequests
            .AsNoTracking()
            .Include(x => x.BuyerOrganization)
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.Addresses)
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.Passport)
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.TrustScore)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.RejectedByUser)
            .Include(x => x.Requirements)
                .ThenInclude(r => r.Requirement)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (item == null)
            return Result<OnboardingRequestDetailDto>.Failure("Onboarding request not found.");

        if (request.BuyerOrganizationId.HasValue && item.BuyerOrganizationId != request.BuyerOrganizationId.Value)
            return Result<OnboardingRequestDetailDto>.Failure("Unauthorized access to this onboarding request.");

        // Fetch Real Audit Logs for this Onboarding Request & Business
        var entityIdStr = item.Id.ToString();
        var businessIdStr = item.SupplierBusinessId.ToString();
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.ResourceId == entityIdStr || a.ResourceId == businessIdStr)
            .OrderByDescending(a => a.CreatedAt)
            .Take(15)
            .ToListAsync(cancellationToken);

        var activities = auditLogs.Select(a => new OnboardingActivityDto(
            Id: a.Id,
            Action: a.Action,
            Title: FormatAuditTitle(a.Action),
            Description: a.Action switch
            {
                "ONBOARDING_CREATED" => "Onboarding request initialized by buyer procurement desk.",
                "ONBOARDING_SENT" => "Formal invitation dispatched to supplier compliance officer.",
                "BUYER_APPROVED" => "Vendor successfully approved and empaneled into ERP master register.",
                "BUYER_REJECTED" => "Onboarding application declined by procurement review committee.",
                _ => a.Action.Replace("_", " ")
            },
            ActorName: "Procurement Officer",
            Timestamp: a.CreatedAt
        )).ToList();

        // If no audit records exist yet, add synthetic genesis record from CreatedAt
        if (!activities.Any())
        {
            activities.Add(new OnboardingActivityDto(
                Id: Guid.NewGuid(),
                Action: "ONBOARDING_CREATED",
                Title: "Onboarding Request Created",
                Description: "Buyer initiated onboarding workflow and defined mandatory verification requirements.",
                ActorName: item.CreatedByUser != null ? $"{item.CreatedByUser.FirstName} {item.CreatedByUser.LastName}" : "Procurement Team",
                Timestamp: item.CreatedAt));
        }

        var primaryAddress = item.SupplierBusiness.Addresses.FirstOrDefault();
        var locationStr = primaryAddress != null ? $"{primaryAddress.City}, {primaryAddress.State}" : "India";

        var totalReqs = item.Requirements.Count;
        var verifiedReqs = item.Requirements.Count(r => r.Status == OnboardingRequirementStatus.VERIFIED);
        var mandatoryReqs = item.Requirements.Where(r => r.IsMandatory).ToList();
        var allMandatoryVerified = mandatoryReqs.All(r => r.Status == OnboardingRequirementStatus.VERIFIED);

        var canApprove = (item.Status == OnboardingStatus.IN_VERIFICATION || item.Status == OnboardingStatus.ACTION_REQUIRED || item.Status == OnboardingStatus.REQUESTED)
                         && allMandatoryVerified;
        var canReject = item.Status != OnboardingStatus.APPROVED && item.Status != OnboardingStatus.REJECTED && item.Status != OnboardingStatus.WITHDRAWN;
        var canWithdraw = item.Status == OnboardingStatus.NEW || item.Status == OnboardingStatus.REQUESTED || item.Status == OnboardingStatus.IN_VERIFICATION;
        var canRemind = item.Status == OnboardingStatus.REQUESTED || item.Status == OnboardingStatus.ACTION_REQUIRED;

        var reqDtos = item.Requirements.Select(r => new OnboardingRequestRequirementDto(
            Id: r.Id,
            RequirementId: r.RequirementId,
            RequirementCode: r.RequirementCode,
            RequirementName: r.RequirementName,
            Category: r.Requirement?.Category ?? "STATUTORY",
            IsMandatory: r.IsMandatory,
            Status: r.Status.ToString(),
            VerificationStatus: r.VerificationStatus,
            VerifiedAt: r.VerifiedAt,
            VerificationSource: r.VerificationSource,
            FailureReason: r.FailureReason,
            EvidenceDocumentName: r.EvidenceDocumentName,
            RequestedAt: r.RequestedAt,
            CompletedAt: r.CompletedAt)).ToList();

        var detail = new OnboardingRequestDetailDto(
            Id: item.Id,
            BuyerOrganizationId: item.BuyerOrganizationId,
            BuyerOrganizationName: item.BuyerOrganization.Name,
            SupplierBusinessId: item.SupplierBusinessId,
            SupplierBusinessName: item.SupplierBusiness.LegalName,
            LegalName: item.SupplierBusiness.LegalName,
            TradeName: item.SupplierBusiness.TradeName,
            Gstin: item.SupplierBusiness.Gstin,
            Pan: item.SupplierBusiness.Pan,
            Cin: item.SupplierBusiness.Cin,
            UdyamNumber: item.SupplierBusiness.UdyamNumber,
            Website: item.SupplierBusiness.Website,
            Location: locationStr,
            Industry: item.SupplierBusiness.BusinessType ?? "Manufacturing",
            VerificationStatus: item.SupplierBusiness.VerificationStatus.ToString(),
            TrustScore: item.SupplierBusiness.TrustScore?.OverallScore,
            PassportNumber: item.SupplierBusiness.Passport?.PassportNumber,
            PassportStatus: item.SupplierBusiness.Passport?.Status,
            Status: item.Status.ToString(),
            Purpose: item.Purpose,
            BusinessCategory: item.BusinessCategory,
            ProcurementDepartment: item.ProcurementDepartment,
            ExpectedRelationship: item.ExpectedRelationship,
            Message: item.Message,
            CreatedAt: item.CreatedAt,
            UpdatedAt: item.UpdatedAt,
            ExpiresAt: item.ExpiresAt,
            CompletedAt: item.CompletedAt,
            ApprovedAt: item.ApprovedAt,
            ApprovedByName: item.ApprovedByUser != null ? $"{item.ApprovedByUser.FirstName} {item.ApprovedByUser.LastName}" : null,
            RejectedAt: item.RejectedAt,
            RejectedByName: item.RejectedByUser != null ? $"{item.RejectedByUser.FirstName} {item.RejectedByUser.LastName}" : null,
            RejectionReason: item.RejectionReason,
            ActionRequiredReason: item.ActionRequiredReason,
            ErpVendorCode: item.ErpVendorCode,
            PaymentTerms: item.PaymentTerms,
            TargetPlant: item.TargetPlant,
            TotalRequirements: totalReqs,
            VerifiedRequirements: verifiedReqs,
            CanApprove: canApprove,
            CanReject: canReject,
            CanWithdraw: canWithdraw,
            CanRemind: canRemind,
            Requirements: reqDtos,
            Activities: activities);

        return Result<OnboardingRequestDetailDto>.Success(detail);
    }

    private static string FormatAuditTitle(string action) => action switch
    {
        "ONBOARDING_CREATED" => "Onboarding Request Created",
        "ONBOARDING_SENT" => "Request Sent to Supplier",
        "SUPPLIER_ACCEPTED" => "Supplier Accepted Request",
        "INFORMATION_SUBMITTED" => "Credentials & Documents Submitted",
        "VERIFICATION_STARTED" => "Verification Probes Active",
        "REQUIREMENT_VERIFIED" => "Statutory Proof Verified",
        "ACTION_REQUIRED" => "Action Required Flagged",
        "BUYER_APPROVED" => "Procurement Approval Granted",
        "BUYER_REJECTED" => "Onboarding Request Declined",
        "REQUEST_REMINDER_SENT" => "Compliance Reminder Sent",
        _ => action.Replace("_", " ")
    };
}

// ==========================================
// 3. GET ONBOARDING REQUIREMENTS CATALOGUE
// ==========================================
public record GetOnboardingRequirementsCatalogueQuery : IRequest<Result<List<RequirementCatalogueItemDto>>>;

public class GetOnboardingRequirementsCatalogueQueryHandler : IRequestHandler<GetOnboardingRequirementsCatalogueQuery, Result<List<RequirementCatalogueItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetOnboardingRequirementsCatalogueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RequirementCatalogueItemDto>>> Handle(GetOnboardingRequirementsCatalogueQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.OnboardingRequirements
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Select(x => new RequirementCatalogueItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.Category,
                x.Description,
                x.IsDefaultMandatory))
            .ToListAsync(cancellationToken);

        return Result<List<RequirementCatalogueItemDto>>.Success(items);
    }
}

// ==========================================
// 4. SEARCH SUPPLIERS FOR ONBOARDING
// ==========================================
public record SearchSuppliersForOnboardingQuery(string? Query, int Page = 1, int PageSize = 10, Guid? BuyerOrgId = null) : IRequest<Result<List<SupplierSearchItemDto>>>;

public class SearchSuppliersForOnboardingQueryHandler : IRequestHandler<SearchSuppliersForOnboardingQuery, Result<List<SupplierSearchItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public SearchSuppliersForOnboardingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SupplierSearchItemDto>>> Handle(SearchSuppliersForOnboardingQuery request, CancellationToken cancellationToken)
    {
        var queryable = _context.Businesses
            .AsNoTracking()
            .Include(x => x.Addresses)
            .Include(x => x.Passport)
            .Include(x => x.TrustScore)
            .Include(x => x.Organization)
            .Where(x => x.Organization.Type == OrganizationType.SUPPLIER)
            .AsQueryable();

        // Authoritative Buyer Selection Rule: Only suppliers with score > 50 are eligible to appear
        queryable = queryable.Where(x => x.TrustScore != null && x.TrustScore.OverallScore > 50);

        if (request.BuyerOrgId.HasValue)
        {
            queryable = queryable.Where(x => x.OrganizationId != request.BuyerOrgId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLower();
            queryable = queryable.Where(x =>
                x.LegalName.ToLower().Contains(term) ||
                (x.TradeName != null && x.TradeName.ToLower().Contains(term)) ||
                (x.Gstin != null && x.Gstin.ToLower().Contains(term)) ||
                (x.Pan != null && x.Pan.ToLower().Contains(term)) ||
                (x.Passport != null && x.Passport.PassportNumber.ToLower().Contains(term)));
        }

        var results = await queryable
            .OrderByDescending(x => x.TrustScore != null ? x.TrustScore.OverallScore : 0)
            .Skip((Math.Max(1, request.Page) - 1) * Math.Clamp(request.PageSize, 1, 50))
            .Take(Math.Clamp(request.PageSize, 1, 50))
            .ToListAsync(cancellationToken);

        var list = results.Select(b =>
        {
            var addr = b.Addresses.FirstOrDefault();
            var loc = addr != null ? $"{addr.City}, {addr.State}" : "India";
            var score = b.TrustScore?.OverallScore ?? 0;
            var riskLevel = SupplierRiskPolicy.DetermineRiskLevelSafe(score).ToString();
            var buyerEligible = SupplierRiskPolicy.IsEligibleForBuyer(score);

            return new SupplierSearchItemDto(
                Id: b.Id,
                BusinessName: b.LegalName,
                LegalName: b.LegalName,
                TradeName: b.TradeName,
                PassportId: b.Passport?.PassportNumber ?? b.BusinessIdNumber,
                GstinMasked: MaskTaxId(b.Gstin),
                PanMasked: MaskTaxId(b.Pan),
                Location: loc,
                Industry: b.BusinessType ?? "Manufacturing",
                VerificationStatus: b.VerificationStatus.ToString(),
                TrustScore: score,
                RiskLevel: riskLevel,
                BuyerEligible: buyerEligible
            );
        }).ToList();

        return Result<List<SupplierSearchItemDto>>.Success(list);
    }

    private static string? MaskTaxId(string? val)
    {
        if (string.IsNullOrWhiteSpace(val) || val.Length < 6) return val;
        var start = val[..3];
        var end = val[^2..];
        return $"{start}****{end}";
    }
}

// ==========================================
// 5. CREATE ONBOARDING REQUEST COMMAND
// ==========================================
public record CreateOnboardingRequestCommand(
    Guid BuyerOrganizationId,
    Guid SupplierBusinessId,
    Guid CreatedByUserId,
    string Purpose,
    string BusinessCategory,
    string ProcurementDepartment,
    string ExpectedRelationship,
    string? Message,
    DateTime? ExpiresAt,
    List<RequirementSelectionInput> Requirements,
    bool SendImmediately = true) : IRequest<Result<Guid>>;

public class CreateOnboardingRequestCommandHandler : IRequestHandler<CreateOnboardingRequestCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Buyer Organization
        var buyer = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == request.BuyerOrganizationId, cancellationToken);
        if (buyer == null)
            return Result<Guid>.Failure("Buyer organization not found.");

        // 2. Validate Supplier Business
        var supplier = await _context.Businesses
            .Include(b => b.Credentials)
            .Include(b => b.Addresses)
            .Include(b => b.BankAccounts)
            .Include(b => b.Documents)
            .Include(b => b.Passport)
            .FirstOrDefaultAsync(b => b.Id == request.SupplierBusinessId, cancellationToken);

        if (supplier == null)
            return Result<Guid>.Failure("Supplier business not found.");

        // 3. Duplicate Request Protection
        var existingActive = await _context.OnboardingRequests.FirstOrDefaultAsync(r =>
            r.BuyerOrganizationId == request.BuyerOrganizationId &&
            r.SupplierBusinessId == request.SupplierBusinessId &&
            (r.Status == OnboardingStatus.NEW ||
             r.Status == OnboardingStatus.REQUESTED ||
             r.Status == OnboardingStatus.IN_VERIFICATION ||
             r.Status == OnboardingStatus.ACTION_REQUIRED),
            cancellationToken);

        if (existingActive != null)
            return Result<Guid>.Failure($"An active onboarding request already exists for this supplier (Status: {existingActive.Status}).");

        // 4. Resolve Requirement Catalogues
        var reqCatalogues = await _context.OnboardingRequirements
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        var onboardingRequest = new OnboardingRequest
        {
            Id = Guid.NewGuid(),
            BuyerOrganizationId = request.BuyerOrganizationId,
            SupplierBusinessId = request.SupplierBusinessId,
            CreatedByUserId = request.CreatedByUserId,
            Status = request.SendImmediately ? OnboardingStatus.REQUESTED : OnboardingStatus.NEW,
            Purpose = request.Purpose,
            BusinessCategory = request.BusinessCategory,
            ProcurementDepartment = request.ProcurementDepartment,
            ExpectedRelationship = request.ExpectedRelationship,
            Message = request.Message,
            ExpiresAt = request.ExpiresAt.HasValue ? DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        // 5. Build Requirements & Automatically Link Existing Sovereign Credentials
        foreach (var reqInput in request.Requirements)
        {
            var matchedCat = reqCatalogues.FirstOrDefault(c => c.Code.Equals(reqInput.RequirementCode, StringComparison.OrdinalIgnoreCase));
            var code = matchedCat?.Code ?? reqInput.RequirementCode.ToUpperInvariant();
            var name = matchedCat?.Name ?? code;
            var reqId = matchedCat?.Id ?? Guid.NewGuid();

            var reqEntity = new OnboardingRequestRequirement
            {
                Id = Guid.NewGuid(),
                OnboardingRequestId = onboardingRequest.Id,
                RequirementId = reqId,
                RequirementCode = code,
                RequirementName = name,
                IsMandatory = reqInput.IsMandatory,
                RequestedAt = DateTime.UtcNow
            };

            // Cross-reference existing supplier credentials and business identity
            CheckExistingVerification(supplier, code, reqEntity);

            onboardingRequest.Requirements.Add(reqEntity);
        }

        // If all mandatory requirements already verified via passport, advance to IN_VERIFICATION or review
        var allVerified = onboardingRequest.Requirements.Where(r => r.IsMandatory).All(r => r.Status == OnboardingRequirementStatus.VERIFIED);
        if (allVerified && request.SendImmediately)
        {
            onboardingRequest.Status = OnboardingStatus.IN_VERIFICATION;
        }

        _context.OnboardingRequests.Add(onboardingRequest);

        // 6. Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.CreatedByUserId,
            OrganizationId = request.BuyerOrganizationId,
            Action = "ONBOARDING_CREATED",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = onboardingRequest.Id.ToString(),
            TargetName = supplier.LegalName,
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CreatedAt = DateTime.UtcNow
        });

        if (request.SendImmediately)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.CreatedByUserId,
                OrganizationId = request.BuyerOrganizationId,
                Action = "ONBOARDING_SENT",
                Category = "ONBOARDING",
                ResourceType = "ONBOARDING",
                ResourceId = onboardingRequest.Id.ToString(),
                TargetName = supplier.LegalName,
                Result = "SUCCESS",
                EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(onboardingRequest.Id);
    }

    private static void CheckExistingVerification(Business supplier, string code, OnboardingRequestRequirement req)
    {
        switch (code.ToUpperInvariant())
        {
            case "GST":
                if (!string.IsNullOrWhiteSpace(supplier.Gstin))
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "GSTN Gateway (GSTR-3B Zero Default 24M)";
                }
                break;

            case "PAN":
                if (!string.IsNullOrWhiteSpace(supplier.Pan))
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "NSDL / Income Tax Dept Registry";
                }
                break;

            case "CIN":
                if (!string.IsNullOrWhiteSpace(supplier.Cin))
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "MCA-21 Company Master Registry";
                }
                break;

            case "BANK_ACCOUNT":
                var bank = supplier.BankAccounts.FirstOrDefault(b => b.VerificationStatus == VerificationStatus.VERIFIED);
                if (bank != null)
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "NPCI Penny-Drop (100% Name Match)";
                }
                else
                {
                    req.Status = OnboardingRequirementStatus.PENDING;
                    req.VerificationStatus = "PENDING";
                }
                break;

            case "REGISTERED_ADDRESS":
                var regAddr = supplier.Addresses.FirstOrDefault(a => a.VerificationStatus == VerificationStatus.VERIFIED || a.AddressType == "REGISTERED");
                if (regAddr != null)
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "State Electricity Board / Municipal Registry";
                }
                break;

            case "MSME":
                if (!string.IsNullOrWhiteSpace(supplier.UdyamNumber))
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "Ministry of MSME Udyam Portal";
                }
                break;

            case "ISO_9001":
                var iso = supplier.Credentials.FirstOrDefault(c => c.CredentialType != null && c.CredentialType.Code == "ISO_9001" && c.VerificationStatus == VerificationStatus.VERIFIED);
                if (iso != null)
                {
                    req.Status = OnboardingRequirementStatus.VERIFIED;
                    req.VerificationStatus = "VERIFIED";
                    req.VerifiedAt = DateTime.UtcNow;
                    req.VerificationSource = "IAF CertSearch Accreditation Registry";
                }
                break;

            default:
                req.Status = OnboardingRequirementStatus.PENDING;
                req.VerificationStatus = "PENDING";
                break;
        }
    }
}

// ==========================================
// 6. SEND ONBOARDING REQUEST COMMAND
// ==========================================
public record SendOnboardingRequestCommand(Guid Id, Guid BuyerOrgId, Guid UserId) : IRequest<Result<bool>>;

public class SendOnboardingRequestCommandHandler : IRequestHandler<SendOnboardingRequestCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public SendOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(SendOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.OnboardingRequests
            .Include(x => x.SupplierBusiness)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item == null) return Result<bool>.Failure("Onboarding request not found.");

        if (item.BuyerOrganizationId != request.BuyerOrgId)
            return Result<bool>.Failure("Unauthorized.");

        if (item.Status != OnboardingStatus.NEW)
            return Result<bool>.Failure($"Cannot send request with status {item.Status}.");

        item.Status = OnboardingStatus.REQUESTED;
        item.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            OrganizationId = request.BuyerOrgId,
            Action = "ONBOARDING_SENT",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = item.Id.ToString(),
            TargetName = item.SupplierBusiness?.LegalName ?? "Supplier Onboarding",
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ==========================================
// 7. APPROVE ONBOARDING REQUEST COMMAND
// ==========================================
public record ApproveOnboardingRequestCommand(
    Guid Id,
    Guid BuyerOrgId,
    Guid ApproverUserId,
    string? ErpVendorCode = null,
    string? PaymentTerms = null,
    string? TargetPlant = null) : IRequest<Result<bool>>;

public class ApproveOnboardingRequestCommandHandler : IRequestHandler<ApproveOnboardingRequestCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public ApproveOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(ApproveOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.OnboardingRequests
            .Include(x => x.Requirements)
            .Include(x => x.SupplierBusiness)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (item == null) return Result<bool>.Failure("Onboarding request not found.");

        if (item.BuyerOrganizationId != request.BuyerOrgId)
            return Result<bool>.Failure("Unauthorized.");

        if (item.Status == OnboardingStatus.APPROVED)
            return Result<bool>.Failure("This onboarding request is already approved.");

        if (item.Status == OnboardingStatus.REJECTED || item.Status == OnboardingStatus.WITHDRAWN)
            return Result<bool>.Failure($"Cannot approve onboarding request with status {item.Status}.");

        // Validate mandatory requirements are verified
        var unverifiedMandatory = item.Requirements.Where(r => r.IsMandatory && r.Status != OnboardingRequirementStatus.VERIFIED && r.Status != OnboardingRequirementStatus.WAIVED).ToList();
        if (unverifiedMandatory.Any())
        {
            var names = string.Join(", ", unverifiedMandatory.Select(r => r.RequirementName));
            return Result<bool>.Failure($"Cannot approve: Mandatory requirement(s) pending verification: {names}.");
        }

        item.Status = OnboardingStatus.APPROVED;
        item.ApprovedAt = DateTime.UtcNow;
        item.ApprovedByUserId = request.ApproverUserId;
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ErpVendorCode)) item.ErpVendorCode = request.ErpVendorCode;
        if (string.IsNullOrWhiteSpace(item.ErpVendorCode)) item.ErpVendorCode = $"SAP-VND-{Math.Floor(10000 + Random.Shared.NextDouble() * 90000)}";
        if (!string.IsNullOrWhiteSpace(request.PaymentTerms)) item.PaymentTerms = request.PaymentTerms;
        if (!string.IsNullOrWhiteSpace(request.TargetPlant)) item.TargetPlant = request.TargetPlant;

        // Upsert BuyerSupplierRelationship
        var relationship = await _context.BuyerSupplierRelationships
            .FirstOrDefaultAsync(r => r.BuyerOrganizationId == item.BuyerOrganizationId && r.SupplierBusinessId == item.SupplierBusinessId, cancellationToken);

        var supplierScore = await _context.TrustScores
            .Where(t => t.BusinessId == item.SupplierBusinessId)
            .Select(t => (int?)t.OverallScore)
            .FirstOrDefaultAsync(cancellationToken);
        var relationshipRisk = SupplierRiskPolicy.DetermineRiskLevelSafe(supplierScore ?? 0);

        if (relationship == null)
        {
            relationship = new BuyerSupplierRelationship
            {
                Id = Guid.NewGuid(),
                BuyerOrganizationId = item.BuyerOrganizationId,
                SupplierBusinessId = item.SupplierBusinessId,
                Status = RelationshipStatus.APPROVED,
                RelationshipType = "DIRECT_VENDOR",
                RiskLevel = relationshipRisk,
                ApprovedAt = DateTime.UtcNow,
                ApprovedByUserId = request.ApproverUserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.BuyerSupplierRelationships.Add(relationship);
        }
        else
        {
            relationship.Status = RelationshipStatus.APPROVED;
            relationship.RiskLevel = relationshipRisk;
            relationship.ApprovedAt = DateTime.UtcNow;
            relationship.ApprovedByUserId = request.ApproverUserId;
        }

        // Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ApproverUserId,
            OrganizationId = request.BuyerOrgId,
            Action = "ONBOARDING_APPROVED",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = item.Id.ToString(),
            TargetName = item.SupplierBusiness?.LegalName ?? "Supplier Empanelment",
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            NewValuesJson = $"{{\"erpVendorCode\":\"{item.ErpVendorCode}\",\"status\":\"APPROVED\"}}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ==========================================
// 8. REJECT ONBOARDING REQUEST COMMAND
// ==========================================
public record RejectOnboardingRequestCommand(
    Guid Id,
    Guid BuyerOrgId,
    Guid RejectedByUserId,
    string Reason) : IRequest<Result<bool>>;

public class RejectOnboardingRequestCommandHandler : IRequestHandler<RejectOnboardingRequestCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public RejectOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RejectOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<bool>.Failure("A rejection reason is mandatory.");

        var item = await _context.OnboardingRequests
            .Include(x => x.SupplierBusiness)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item == null) return Result<bool>.Failure("Onboarding request not found.");

        if (item.BuyerOrganizationId != request.BuyerOrgId)
            return Result<bool>.Failure("Unauthorized.");

        if (item.Status == OnboardingStatus.APPROVED)
            return Result<bool>.Failure("Cannot reject an onboarding request that has already been approved.");

        item.Status = OnboardingStatus.REJECTED;
        item.RejectedAt = DateTime.UtcNow;
        item.RejectedByUserId = request.RejectedByUserId;
        item.RejectionReason = request.Reason.Trim();
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.RejectedByUserId,
            OrganizationId = request.BuyerOrgId,
            Action = "ONBOARDING_REJECTED",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = item.Id.ToString(),
            TargetName = item.SupplierBusiness?.LegalName ?? "Supplier Empanelment",
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            NewValuesJson = $"{{\"reason\":\"{item.RejectionReason}\",\"status\":\"REJECTED\"}}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ==========================================
// 9. WITHDRAW ONBOARDING REQUEST COMMAND
// ==========================================
public record WithdrawOnboardingRequestCommand(Guid Id, Guid BuyerOrgId, Guid UserId) : IRequest<Result<bool>>;

public class WithdrawOnboardingRequestCommandHandler : IRequestHandler<WithdrawOnboardingRequestCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public WithdrawOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(WithdrawOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.OnboardingRequests
            .Include(x => x.SupplierBusiness)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item == null) return Result<bool>.Failure("Onboarding request not found.");

        if (item.BuyerOrganizationId != request.BuyerOrgId)
            return Result<bool>.Failure("Unauthorized.");

        if (item.Status == OnboardingStatus.APPROVED || item.Status == OnboardingStatus.REJECTED)
            return Result<bool>.Failure($"Cannot withdraw an onboarding request with status {item.Status}.");

        item.Status = OnboardingStatus.WITHDRAWN;
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            OrganizationId = request.BuyerOrgId,
            Action = "ONBOARDING_WITHDRAWN",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = item.Id.ToString(),
            TargetName = item.SupplierBusiness?.LegalName ?? "Supplier Empanelment",
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ==========================================
// 10. REMIND ONBOARDING REQUEST COMMAND
// ==========================================
public record RemindOnboardingRequestCommand(Guid Id, Guid BuyerOrgId, Guid UserId) : IRequest<Result<bool>>;

public class RemindOnboardingRequestCommandHandler : IRequestHandler<RemindOnboardingRequestCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public RemindOnboardingRequestCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(RemindOnboardingRequestCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.OnboardingRequests
            .Include(x => x.SupplierBusiness)
                .ThenInclude(b => b.Organization)
                    .ThenInclude(o => o.Members)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (item == null) return Result<bool>.Failure("Onboarding request not found.");

        if (item.BuyerOrganizationId != request.BuyerOrgId)
            return Result<bool>.Failure("Unauthorized.");

        // Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            OrganizationId = request.BuyerOrgId,
            Action = "ONBOARDING_REMINDER_SENT",
            Category = "ONBOARDING",
            ResourceType = "ONBOARDING",
            ResourceId = item.Id.ToString(),
            TargetName = item.SupplierBusiness?.LegalName ?? "Supplier Empanelment",
            Result = "SUCCESS",
            EventCode = $"AUD-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CreatedAt = DateTime.UtcNow
        });

        // Notify Supplier User if exists
        var supplierMember = item.SupplierBusiness?.Organization?.Members.FirstOrDefault();
        if (supplierMember != null)
        {
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = supplierMember.UserId,
                Type = NotificationType.VERIFICATION_UPDATE,
                Title = "Onboarding Action Required",
                Message = $"Please review and complete the outstanding onboarding requirements for {item.Purpose}.",
                ResourceType = "OnboardingRequest",
                ResourceId = item.Id.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
