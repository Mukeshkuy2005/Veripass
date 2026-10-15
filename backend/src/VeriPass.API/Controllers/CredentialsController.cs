using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Credentials;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CredentialsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CredentialsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("/api/v1/businesses/{businessId:guid}/credentials")]
    public async Task<IActionResult> GetBusinessCredentials(Guid businessId)
    {
        var result = await _mediator.Send(new GetCredentialsQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("/api/v1/businesses/{businessId:guid}/credentials")]
    public async Task<IActionResult> CreateCredentialForBusiness(Guid businessId, [FromBody] CreateBusinessCredentialRequest request)
    {
        var command = new CreateCredentialCommand(businessId, request.CredentialTypeId, request.CredentialNumber, request.Issuer, request.IssuedDate, request.ExpiryDate);
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { credentialId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCredential(Guid id)
    {
        var result = await _mediator.Send(new GetCredentialQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCredential([FromBody] CreateCredentialCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { credentialId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCredential(Guid id, [FromBody] UpdateCredentialCommand command)
    {
        if (id != command.CredentialId) return BadRequest("Credential ID mismatch.");
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCredential(Guid id)
    {
        var result = await _mediator.Send(new DeleteCredentialCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitCredential(Guid id)
    {
        var result = await _mediator.Send(new SubmitCredentialCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> VerifyCredential(Guid id)
    {
        var result = await _mediator.Send(new VerifyCredentialCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record CreateBusinessCredentialRequest(Guid CredentialTypeId, string CredentialNumber, string? Issuer, DateTime? IssuedDate, DateTime? ExpiryDate);
}
