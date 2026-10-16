using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Verification;

namespace VeriPass.API.Controllers;

[ApiController]
[AllowAnonymous]
public class VerifierDashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public VerifierDashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("/api/v1/verifier/dashboard")]
    [HttpGet("/api/verifier/dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetVerifierDashboardQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }
}
