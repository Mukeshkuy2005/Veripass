using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Relationships;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/verification-requests")]
[Authorize]
public class VerificationRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public VerificationRequestsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] Guid? businessId, [FromQuery] Guid? buyerOrganizationId)
    {
        var effectiveBuyerOrgId = buyerOrganizationId ?? (businessId.HasValue ? null : _currentUserService.OrganizationId);
        if (!businessId.HasValue && !effectiveBuyerOrgId.HasValue)
        {
            return Ok(new List<object>());
        }
        var result = await _mediator.Send(new GetAllVerificationRequestsQuery(businessId, effectiveBuyerOrgId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetVerificationRequestByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateVerificationRequestDto dto)
    {
        var buyerOrgId = dto.BuyerOrganizationId ?? _currentUserService.OrganizationId;
        var userId = dto.RequestedByUserId ?? _currentUserService.UserId;

        if (!buyerOrgId.HasValue || !userId.HasValue)
        {
            return BadRequest(new { message = "Authenticated buyer organization and user required." });
        }

        var command = new CreateVerificationRequestCommand(
            buyerOrgId.Value,
            dto.SupplierBusinessId,
            userId.Value,
            dto.RequestedItems ?? new List<string>(),
            dto.Reason,
            dto.RequestType ?? "CREDENTIAL_UPDATE"
        );

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { requestId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/approve")]
    [AllowAnonymous]
    public async Task<IActionResult> Approve(Guid id, [FromBody] VerificationRequestResponse? body)
    {
        var result = await _mediator.Send(new ApproveVerificationRequestCommand(id, body?.Notes));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/reject")]
    [AllowAnonymous]
    public async Task<IActionResult> Reject(Guid id, [FromBody] VerificationRequestResponse? body)
    {
        var result = await _mediator.Send(new RejectVerificationRequestCommand(id, body?.Notes));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/cancel")]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _mediator.Send(new CancelVerificationRequestCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record VerificationRequestResponse(string? Notes);

    public record CreateVerificationRequestDto(
        Guid? BuyerOrganizationId,
        Guid SupplierBusinessId,
        Guid? RequestedByUserId,
        List<string>? RequestedItems,
        string? Reason,
        string? RequestType);
}
