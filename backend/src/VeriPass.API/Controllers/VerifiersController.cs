using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Admin;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/admin/[controller]")]
[Authorize(Roles = "ADMIN")]
public class VerifiersController : ControllerBase
{
    private readonly IMediator _mediator;

    public VerifiersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetVerifiers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetAdminVerifiersQuery(search, status, page, pageSize));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVerifierById([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new GetAdminVerifierByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> CreateVerifier([FromBody] CreateVerifierRequest request)
    {
        var command = new CreateVerifierCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.MustChangePassword
        );
        var result = await _mediator.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVerifierById), new { id = result.Value.Id }, result.Value)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateVerifier([FromRoute] Guid id, [FromBody] UpdateVerifierRequest request)
    {
        var command = new UpdateVerifierCommand(id, request.FirstName, request.LastName, request.Phone);
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateVerifier([FromRoute] Guid id, [FromBody] DeactivateVerifierRequest? request)
    {
        var result = await _mediator.Send(new DeactivateVerifierCommand(id, request?.Reason));
        return result.IsSuccess ? Ok(new { success = true, message = "Verifier deactivated successfully." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateVerifier([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new ReactivateVerifierCommand(id));
        return result.IsSuccess ? Ok(new { success = true, message = "Verifier reactivated successfully." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> SuspendVerifier([FromRoute] Guid id, [FromBody] SuspendVerifierRequest request)
    {
        var result = await _mediator.Send(new SuspendVerifierCommand(id, request.Reason));
        return result.IsSuccess ? Ok(new { success = true, message = "Verifier suspended successfully." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword([FromRoute] Guid id, [FromBody] ResetVerifierPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetVerifierPasswordCommand(id, request.NewPassword, request.MustChangePassword));
        return result.IsSuccess ? Ok(new { success = true, message = "Verifier password reset successfully." }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteVerifier([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new DeleteVerifierCommand(id));
        return result.IsSuccess ? Ok(new { success = true, message = "Verifier deleted successfully (soft-deleted, audit intact)." }) : BadRequest(new { message = result.Error });
    }

    public record CreateVerifierRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string? Phone,
        bool MustChangePassword = true);

    public record UpdateVerifierRequest(
        string FirstName,
        string LastName,
        string? Phone);

    public record DeactivateVerifierRequest(string? Reason);
    public record SuspendVerifierRequest(string Reason);
    public record ResetVerifierPasswordRequest(string NewPassword, bool MustChangePassword = true);
}
