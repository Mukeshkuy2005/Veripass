using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Verification;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/verifier/[controller]")]
[AllowAnonymous]
public class VerifierProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public VerifierProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HttpGet("/api/verifier/profile")]
    [HttpGet("/api/v1/verifier/profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string? range = "30d")
    {
        var result = await _mediator.Send(new GetVerifierProfileQuery(range));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("activity")]
    [HttpGet("/api/verifier/profile/activity")]
    [HttpGet("/api/v1/verifier/profile/activity")]
    public async Task<IActionResult> GetActivity([FromQuery] string? range = "30d")
    {
        var result = await _mediator.Send(new GetVerifierActivityQuery(range ?? "30d"));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("sessions")]
    [HttpGet("/api/verifier/profile/sessions")]
    [HttpGet("/api/v1/verifier/profile/sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var result = await _mediator.Send(new GetVerifierSessionsQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("sessions/revoke-others")]
    [HttpPost("/api/verifier/profile/sessions/revoke-others")]
    [HttpPost("/api/v1/verifier/profile/sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions()
    {
        var result = await _mediator.Send(new RevokeVerifierOtherSessionsCommand());
        return result.IsSuccess
            ? Ok(new { success = true, revokedCount = result.Value, message = "Successfully signed out of all other sessions." })
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("sessions/{sessionId:guid}/revoke")]
    [HttpPost("/api/verifier/profile/sessions/{sessionId:guid}/revoke")]
    [HttpPost("/api/v1/verifier/profile/sessions/{sessionId:guid}/revoke")]
    public async Task<IActionResult> RevokeSession([FromRoute] Guid sessionId)
    {
        var result = await _mediator.Send(new RevokeVerifierSessionCommand(sessionId));
        return result.IsSuccess
            ? Ok(new { success = true, message = "Session revoked successfully." })
            : BadRequest(new { message = result.Error });
    }

    [HttpGet("login-history")]
    [HttpGet("/api/verifier/profile/login-history")]
    [HttpGet("/api/v1/verifier/profile/login-history")]
    public async Task<IActionResult> GetLoginHistory()
    {
        var result = await _mediator.Send(new GetVerifierLoginHistoryQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("mfa/toggle")]
    [HttpPost("/api/verifier/profile/mfa/toggle")]
    [HttpPost("/api/v1/verifier/profile/mfa/toggle")]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleVerifierMfaRequest request)
    {
        var result = await _mediator.Send(new ToggleVerifierMfaCommand(request.Enable));
        return result.IsSuccess
            ? Ok(new { success = true, mfaEnabled = result.Value, message = request.Enable ? "MFA enabled successfully." : "MFA disabled successfully." })
            : BadRequest(new { message = result.Error });
    }

    public record ToggleVerifierMfaRequest(bool Enable);
}
