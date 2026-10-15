using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Organizations;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrganizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrganization(Guid id)
    {
        var result = await _mediator.Send(new GetOrganizationQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationCommand command)
    {
        if (id != command.OrganizationId) return BadRequest("Organization ID mismatch.");
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}/dashboard")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDashboard(Guid id)
    {
        var result = await _mediator.Send(new GetOrganizationDashboardQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var result = await _mediator.Send(new GetMembersQuery(id));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var result = await _mediator.Send(new AddMemberCommand(id, request.Email, request.RoleName));
        return result.IsSuccess ? Ok(new { memberId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id}/members/{memberId}")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberId, [FromBody] UpdateMemberRoleRequest request)
    {
        var result = await _mediator.Send(new UpdateMemberRoleCommand(id, memberId, request.RoleName));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        var result = await _mediator.Send(new RemoveMemberCommand(id, memberId));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record AddMemberRequest(string Email, string RoleName = "BUYER");
    public record UpdateMemberRoleRequest(string RoleName);
}
