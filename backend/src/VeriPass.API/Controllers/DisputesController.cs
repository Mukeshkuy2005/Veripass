using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Disputes;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DisputesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public DisputesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDisputes([FromQuery] Guid businessId)
    {
        var result = await _mediator.Send(new GetDisputesQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDispute(Guid id)
    {
        var result = await _mediator.Send(new GetDisputeByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDisputeRequest request)
    {
        var command = new CreateDisputeCommand(
            request.BusinessId,
            _currentUserService.UserId ?? Guid.Empty,
            request.Subject,
            request.Description,
            request.VerificationCaseId
        );

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { disputeId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] RespondDisputeRequest request)
    {
        var result = await _mediator.Send(new RespondToDisputeCommand(id, request.Response));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveDisputeRequest request)
    {
        var result = await _mediator.Send(new ResolveDisputeCommand(id, request.Resolution));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record CreateDisputeRequest(Guid BusinessId, string Subject, string Description, Guid? VerificationCaseId = null);
    public record RespondDisputeRequest(string Response);
    public record ResolveDisputeRequest(string Resolution);
}
