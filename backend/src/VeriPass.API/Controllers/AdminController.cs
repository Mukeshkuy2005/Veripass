using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Admin;
using VeriPass.Application.Compliance;
using VeriPass.Domain.Enums;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // Businesses Management
    [HttpGet("businesses")]
    public async Task<IActionResult> GetBusinesses(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetAdminBusinessesQuery(search, status, page, pageSize));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("businesses/{id}")]
    public async Task<IActionResult> GetBusinessById(string id)
    {
        var result = await _mediator.Send(new GetAdminBusinessByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("businesses/{id}/suspend")]
    public async Task<IActionResult> SuspendBusiness(string id, [FromBody] ActionReasonRequest? request)
    {
        var result = await _mediator.Send(new SuspendBusinessCommand(id, request?.Reason ?? "Suspended by admin compliance"));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("businesses/{id}/activate")]
    public async Task<IActionResult> ActivateBusiness(string id)
    {
        var result = await _mediator.Send(new ActivateBusinessCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("businesses/{id}/flag")]
    public async Task<IActionResult> FlagBusiness(string id, [FromBody] ActionReasonRequest? request)
    {
        var result = await _mediator.Send(new FlagBusinessCommand(id, request?.Reason ?? "Flagged by admin compliance"));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    // Buyers
    [HttpGet("buyers")]
    public async Task<IActionResult> GetBuyers()
    {
        var result = await _mediator.Send(new GetAdminBuyersQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // Verifications
    [HttpGet("verifications")]
    public async Task<IActionResult> GetVerifications([FromQuery] string? status)
    {
        var result = await _mediator.Send(new GetAdminVerificationsQuery(status));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // Credential Types
    [HttpGet("credential-types")]
    public async Task<IActionResult> GetCredentialTypes()
    {
        var result = await _mediator.Send(new GetCredentialTypesQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("credential-types")]
    public async Task<IActionResult> CreateCredentialType([FromBody] CreateCredentialTypeCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { id = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPut("credential-types/{id}")]
    public async Task<IActionResult> UpdateCredentialType(Guid id, [FromBody] UpdateCredentialTypeRequest request)
    {
        var command = new UpdateCredentialTypeCommand(
            id, request.Name, request.Category, request.IsRequired, request.RequiresExpiry, request.VerificationProvider, request.Description, request.IsActive);
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    // Rules
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        var result = await _mediator.Send(new GetComplianceRulesQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateComplianceRuleCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { id = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPut("rules/{id}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateComplianceRuleRequest request)
    {
        var command = new UpdateComplianceRuleCommand(
            id,
            request.Name,
            request.Category,
            request.Severity,
            request.Description,
            request.IsActive,
            request.CredentialType,
            request.ValidationType,
            request.IsRequired,
            request.DefaultWeight,
            request.RuleExpressionJson);
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    // Reports & Audit Logs
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports()
    {
        var result = await _mediator.Send(new GetAdminReportsQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetAuditLogsQuery(page, pageSize));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // Users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _mediator.Send(new GetAdminUsersQuery());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var result = await _mediator.Send(new UpdateAdminUserCommand(id, request.Status));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record ActionReasonRequest(string? Reason);
    public record UpdateCredentialTypeRequest(string Name, string Category, bool IsRequired, bool RequiresExpiry, string? VerificationProvider, string? Description, bool IsActive);
    public record UpdateComplianceRuleRequest(
        string Name,
        string Category,
        ComplianceSeverity Severity,
        string Description,
        bool IsActive,
        string? CredentialType = null,
        string? ValidationType = null,
        bool IsRequired = true,
        int DefaultWeight = 0,
        string? RuleExpressionJson = null);
    public record UpdateUserRequest(string Status);
}
