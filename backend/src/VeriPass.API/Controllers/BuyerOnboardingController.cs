using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.BuyerOnboarding;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/buyer/onboarding")]
[Authorize]
public class BuyerOnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    public BuyerOnboardingController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    private Guid? GetEffectiveBuyerOrgId()
    {
        return _currentUserService.OrganizationId;
    }

    private Guid? GetEffectiveUserId()
    {
        return _currentUserService.UserId;
    }

    /// <summary>
    /// Gets paginated, filtered onboarding requests with aggregate status counts.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? requestedBy = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] string? sortDirection = "desc")
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        if (!buyerOrgId.HasValue)
        {
            return Ok(new OnboardingPagedListDto(
                new List<OnboardingRequestSummaryDto>(),
                0, page, pageSize,
                new OnboardingStatusCountsDto(0, 0, 0, 0, 0, 0, 0, 0, 0)
            ));
        }

        var query = new GetOnboardingListQuery(
            buyerOrgId.Value,
            page,
            pageSize,
            search,
            status,
            category,
            requestedBy,
            sortBy,
            sortDirection);

        var result = await _mediator.Send(query);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Gets the standard onboarding requirement catalogue for requirement selection.
    /// </summary>
    [HttpGet("requirements")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRequirementsCatalogue()
    {
        var result = await _mediator.Send(new GetOnboardingRequirementsCatalogueQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Searches eligible sovereign suppliers to start an onboarding workflow.
    /// </summary>
    [HttpGet("suppliers/search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchSuppliers([FromQuery] string? q = null)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var result = await _mediator.Send(new SearchSuppliersForOnboardingQuery(q, 1, 20, buyerOrgId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Gets a single comprehensive onboarding request workspace by ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var result = await _mediator.Send(new GetOnboardingDetailQuery(id, buyerOrgId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    /// <summary>
    /// Creates a new buyer onboarding request with selected statutory/quality requirements.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateOnboardingRequestApiDto dto)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }

        var requirements = dto.Requirements?.Select(r => new RequirementSelectionInput(
            r.RequirementCode,
            r.IsMandatory
        )).ToList() ?? new List<RequirementSelectionInput>();

        var command = new CreateOnboardingRequestCommand(
            buyerOrgId.Value,
            dto.SupplierBusinessId,
            userId.Value,
            dto.Purpose,
            dto.BusinessCategory,
            dto.ProcurementDepartment,
            dto.ExpectedRelationship,
            dto.Message,
            dto.ExpiresAt,
            requirements,
            dto.SendImmediately);

        var result = await _mediator.Send(command);
        return result.IsSuccess
            ? Ok(new { id = result.Value, message = "Onboarding request successfully created and dispatched." })
            : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Sends an onboarding request to the supplier if not already sent.
    /// </summary>
    [HttpPost("{id}/send")]
    [AllowAnonymous]
    public async Task<IActionResult> Send(Guid id)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }
        var result = await _mediator.Send(new SendOnboardingRequestCommand(id, buyerOrgId.Value, userId.Value));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Approves the onboarding request for procurement empanelment.
    /// </summary>
    [HttpPost("{id}/approve")]
    [AllowAnonymous]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveOnboardingApiDto? dto)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }
        var result = await _mediator.Send(new ApproveOnboardingRequestCommand(
            id,
            buyerOrgId.Value,
            userId.Value,
            dto?.ErpVendorCode,
            dto?.PaymentTerms,
            dto?.TargetPlant));

        return result.IsSuccess ? Ok(new { success = true, message = "Supplier successfully approved and empanelled." }) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Rejects the onboarding request with justification.
    /// </summary>
    [HttpPost("{id}/reject")]
    [AllowAnonymous]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOnboardingApiDto dto)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }
        var result = await _mediator.Send(new RejectOnboardingRequestCommand(
            id,
            buyerOrgId.Value,
            userId.Value,
            dto?.Reason ?? "Does not meet procurement specifications."));

        return result.IsSuccess ? Ok(new { success = true, message = "Onboarding request rejected." }) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Withdraws an active onboarding request.
    /// </summary>
    [HttpPost("{id}/withdraw")]
    [AllowAnonymous]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }
        var result = await _mediator.Send(new WithdrawOnboardingRequestCommand(
            id,
            buyerOrgId.Value,
            userId.Value));

        return result.IsSuccess ? Ok(new { success = true, message = "Onboarding request withdrawn." }) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Sends a reminder notification to the supplier.
    /// </summary>
    [HttpPost("{id}/remind")]
    [AllowAnonymous]
    public async Task<IActionResult> Remind(Guid id)
    {
        var buyerOrgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }
        var result = await _mediator.Send(new RemindOnboardingRequestCommand(id, buyerOrgId.Value, userId.Value));
        return result.IsSuccess ? Ok(new { success = true, message = "Reminder sent to supplier." }) : BadRequest(new { message = result.Error });
    }
}

public class CreateOnboardingRequestApiDto
{
    public Guid SupplierBusinessId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string BusinessCategory { get; set; } = string.Empty;
    public string ProcurementDepartment { get; set; } = string.Empty;
    public string ExpectedRelationship { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<RequirementSelectionApiDto> Requirements { get; set; } = new();
    public bool SendImmediately { get; set; } = true;
}

public class RequirementSelectionApiDto
{
    public string RequirementCode { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
}

public class ApproveOnboardingApiDto
{
    public string? ErpVendorCode { get; set; }
    public string? PaymentTerms { get; set; }
    public string? TargetPlant { get; set; }
}

public class RejectOnboardingApiDto
{
    public string Reason { get; set; } = string.Empty;
}
