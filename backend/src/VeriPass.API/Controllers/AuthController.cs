using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Auth;
using VeriPass.Application.Common.Interfaces;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { message = result.Error });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { message = result.Error });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var result = await _mediator.Send(new LogoutCommand(request?.RefreshToken));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Email));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _mediator.Send(new VerifyEmailCommand(request.Email, request.Token));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        var result = await _mediator.Send(new SendOtpCommand(request.Phone));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await _mediator.Send(new VerifyOtpCommand(request.Phone, request.Otp));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await _mediator.Send(new GetCurrentUserQuery());
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPut("profile")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCurrentUserProfileCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("change-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _mediator.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword));
        return result.IsSuccess ? Ok(new { success = true, message = "Password changed successfully." }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("mfa/toggle")]
    [AllowAnonymous]
    public async Task<IActionResult> ToggleMfa([FromBody] ToggleMfaCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("mfa/send-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMfaOtp([FromBody] SendMfaOtpRequest? request)
    {
        var result = await _mediator.Send(new SendMfaOtpCommand(request?.Email));
        return result.IsSuccess
            ? Ok(new { success = true, message = "Verification code dispatched to your Gmail." })
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("mfa/verify-login")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfaLogin([FromBody] VerifyMfaLoginRequest request)
    {
        var result = await _mediator.Send(new VerifyMfaLoginCommand(request.Email, request.Code, request.Ticket));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("sessions/revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { success = true, message = "Session revoked successfully." }) : BadRequest(new { message = result.Error });
    }

    public record LogoutRequest(string? RefreshToken);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Email, string Token, string NewPassword);
    public record VerifyEmailRequest(string Email, string Token);
    public record SendOtpRequest(string Phone);
    public record VerifyOtpRequest(string Phone, string Otp);
    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
    public record SendMfaOtpRequest(string? Email);
    public record VerifyMfaLoginRequest(string Email, string Code, string? Ticket);
}
