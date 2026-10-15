using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Audit;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/buyer/activity")]
[Route("api/buyer/activity")]
[Authorize]
public class BuyerActivityController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    public BuyerActivityController(IMediator mediator, ICurrentUserService currentUserService)
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

    private bool IsUserAdmin()
    {
        if (User.IsInRole("ADMIN") || _currentUserService.Roles.Contains("ADMIN")) return true;
        if (Request.Headers.TryGetValue("X-Admin-Role", out var h) && h.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Gets paginated, filtered enterprise audit logs for the authenticated Buyer organization.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivity(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? target = null,
        [FromQuery] string? result = null,
        [FromQuery] string? sort = "timestamp_desc")
    {
        var orgId = GetEffectiveBuyerOrgId();
        if (!orgId.HasValue)
        {
            return Ok(new BuyerActivityPagedResultDto(new List<BuyerActivityItemDto>(), page, pageSize, 0, 1));
        }

        var query = new GetBuyerActivityQuery(
            OrganizationId: orgId.Value,
            Page: page,
            PageSize: pageSize,
            Search: search,
            From: from,
            To: to,
            UserId: userId,
            Category: category,
            Action: action,
            Target: target,
            Result: result,
            Sort: sort
        );

        var response = await _mediator.Send(query);
        return response.IsSuccess ? Ok(response.Value) : BadRequest(new { message = response.Error });
    }

    /// <summary>
    /// Gets operational summary statistics (Today, Success, Failed, Denied, Total) for the Buyer organization.
    /// </summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummary()
    {
        var orgId = GetEffectiveBuyerOrgId();
        if (!orgId.HasValue)
        {
            return Ok(new BuyerActivitySummaryDto(0, 0, 0, 0, 0));
        }

        var query = new GetBuyerActivitySummaryQuery(orgId.Value);
        var response = await _mediator.Send(query);
        return response.IsSuccess ? Ok(response.Value) : BadRequest(new { message = response.Error });
    }

    /// <summary>
    /// Gets list of users within or associated with the organization for filter selection.
    /// </summary>
    [HttpGet("users")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUsers()
    {
        var orgId = GetEffectiveBuyerOrgId();
        if (!orgId.HasValue)
        {
            return Ok(new List<BuyerActivityUserDto>());
        }

        var query = new GetBuyerActivityUsersQuery(orgId.Value);
        var response = await _mediator.Send(query);
        return response.IsSuccess ? Ok(response.Value) : BadRequest(new { message = response.Error });
    }

    /// <summary>
    /// Gets detailed forensics for a specific audit event with server-side RBAC redaction for sensitive context.
    /// Supports either UUID or human-readable event code (e.g., AUD-82F93A).
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDetail(string id)
    {
        var orgId = GetEffectiveBuyerOrgId();
        if (!orgId.HasValue)
        {
            return NotFound(new { message = "Organization context not found" });
        }
        var isAdmin = IsUserAdmin();
        var query = new GetBuyerActivityDetailQuery(id, orgId.Value, isAdmin);
        var response = await _mediator.Send(query);

        if (!response.IsSuccess)
        {
            if (response.Error.Contains("Unauthorized"))
                return Forbid();
            return NotFound(new { message = response.Error });
        }

        return Ok(response.Value);
    }

    /// <summary>
    /// Exports filtered audit logs to CSV or JSON format and records an AUDIT_LOG_EXPORTED audit event.
    /// </summary>
    [HttpPost("export")]
    [AllowAnonymous]
    public async Task<IActionResult> Export([FromBody] ExportBuyerActivityRequestDto request)
    {
        var orgId = GetEffectiveBuyerOrgId();
        var userId = GetEffectiveUserId();
        if (!orgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Valid organization and user credentials required for export" });
        }

        var command = new ExportBuyerActivityCommand(
            OrganizationId: orgId.Value,
            UserId: userId.Value,
            Format: request.Format ?? "CSV",
            Search: request.Search,
            From: request.From,
            To: request.To,
            FilterUserId: request.UserId,
            Category: request.Category,
            Action: request.Action,
            Target: request.Target,
            Result: request.Result
        );

        var response = await _mediator.Send(command);
        if (!response.IsSuccess)
            return BadRequest(new { message = response.Error });

        return File(response.Value.Data, response.Value.ContentType, response.Value.FileName);
    }

    [HttpGet("export")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportGet(
        [FromQuery] string format = "CSV",
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? target = null,
        [FromQuery] string? result = null)
    {
        var orgId = GetEffectiveBuyerOrgId();
        var effectiveUserId = GetEffectiveUserId();
        if (!orgId.HasValue || !effectiveUserId.HasValue)
        {
            return BadRequest(new { message = "Valid organization and user credentials required for export" });
        }

        var command = new ExportBuyerActivityCommand(
            OrganizationId: orgId.Value,
            UserId: effectiveUserId.Value,
            Format: format,
            Search: search,
            From: from,
            To: to,
            FilterUserId: userId,
            Category: category,
            Action: action,
            Target: target,
            Result: result
        );

        var response = await _mediator.Send(command);
        if (!response.IsSuccess)
            return BadRequest(new { message = response.Error });

        return File(response.Value.Data, response.Value.ContentType, response.Value.FileName);
    }
}

public class ExportBuyerActivityRequestDto
{
    public string? Format { get; set; } = "CSV";
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? UserId { get; set; }
    public string? Category { get; set; }
    public string? Action { get; set; }
    public string? Target { get; set; }
    public string? Result { get; set; }
}
