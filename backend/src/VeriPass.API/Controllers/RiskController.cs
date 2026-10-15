using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Risk;

namespace VeriPass.API.Controllers;

[ApiController]
[Authorize]
public class RiskController : ControllerBase
{
    private readonly IMediator _mediator;

    public RiskController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/v1/businesses/{businessId}/risk")]
    public async Task<IActionResult> GetBusinessRisk(Guid businessId)
    {
        var result = await _mediator.Send(new GetBusinessRiskOverviewQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("api/v1/businesses/{businessId:guid}/risk/signals")]
    public async Task<IActionResult> GetSignals([FromRoute] Guid businessId)
    {
        var result = await _mediator.Send(new GetRiskSignalsQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("api/v1/admin/risk")]
    public async Task<IActionResult> GetAdminRisk()
    {
        var result = await _mediator.Send(new GetAdminRiskSignalsQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("api/v1/admin/risk/signals/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveSignal(Guid id, [FromBody] ResolveRiskRequest? request)
    {
        var result = await _mediator.Send(new ResolveRiskSignalCommand(id, request?.Notes ?? "Resolved by risk compliance officer"));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record ResolveRiskRequest(string? Notes);
}
