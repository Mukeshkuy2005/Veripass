using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Suppliers;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class ShortlistsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    public ShortlistsController(IMediator mediator, ICurrentUserService currentUserService, IAuditService auditService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetShortlists([FromQuery] Guid? organizationId = null)
    {
        var orgId = organizationId ?? _currentUserService.OrganizationId;
        if (!orgId.HasValue) return Ok(new List<object>());
        var result = await _mediator.Send(new GetShortlistsQuery(orgId.Value));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> CreateShortlist([FromBody] CreateShortlistRequest request, [FromQuery] Guid? organizationId = null)
    {
        var orgId = organizationId ?? _currentUserService.OrganizationId;
        if (!orgId.HasValue) return BadRequest(new { message = "Authenticated organization is required." });
        var result = await _mediator.Send(new CreateShortlistCommand(orgId.Value, request.Name, request.Description));
        return result.IsSuccess ? Ok(new { shortlistId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetShortlist(Guid id)
    {
        var result = await _mediator.Send(new GetShortlistByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("{id}/suppliers/{businessId}")]
    public async Task<IActionResult> AddSupplier(Guid id, Guid businessId, [FromBody] AddSupplierRequest? request)
    {
        var result = await _mediator.Send(new AddToShortlistCommand(id, businessId, request?.Notes));
        if (result.IsSuccess)
        {
            var orgId = _currentUserService.OrganizationId ?? Guid.Empty;
            var userId = _currentUserService.UserId ?? Guid.Empty;
            if (orgId != Guid.Empty)
            {
                await _auditService.RecordAsync(new RecordAuditOptions(
                    Action: "SUPPLIER_SHORTLISTED",
                    Category: "SUPPLIER",
                    TargetType: "SUPPLIER",
                    TargetId: businessId.ToString(),
                    TargetName: $"Supplier Added to Shortlist {id}",
                    Result: "SUCCESS",
                    OverrideOrganizationId: orgId,
                    OverrideUserId: userId
                ));
            }
        }
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id}/suppliers/{businessId}")]
    public async Task<IActionResult> RemoveSupplier(Guid id, Guid businessId)
    {
        var result = await _mediator.Send(new RemoveFromShortlistCommand(id, businessId));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShortlist(Guid id, [FromQuery] Guid? organizationId = null)
    {
        var orgId = organizationId ?? _currentUserService.OrganizationId;
        if (!orgId.HasValue) return BadRequest(new { message = "Organization ID required." });
        var result = await _mediator.Send(new DeleteShortlistCommand(id, orgId.Value));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record CreateShortlistRequest(string Name, string? Description);
    public record AddSupplierRequest(string? Notes);
}

