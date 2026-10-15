using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Compliance;
using VeriPass.Application.Suppliers;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComplianceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard/{businessId:guid}")]
    public async Task<IActionResult> GetDashboard(Guid businessId)
    {
        var result = await _mediator.Send(new GetComplianceDashboardQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("events/{businessId:guid}")]
    [HttpGet("/api/v1/businesses/{businessId:guid}/compliance/events")]
    public async Task<IActionResult> GetEvents(Guid businessId)
    {
        var result = await _mediator.Send(new GetComplianceEventsQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("calendar/{businessId:guid}")]
    [HttpGet("/api/v1/businesses/{businessId:guid}/compliance/calendar")]
    public async Task<IActionResult> GetCalendar(Guid businessId, [FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _mediator.Send(new GetComplianceCalendarQuery(businessId, month, year));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("events/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var result = await _mediator.Send(new AcknowledgeComplianceEventCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }
}
