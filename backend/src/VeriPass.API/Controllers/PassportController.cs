using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Passport;

namespace VeriPass.API.Controllers;

[ApiController]
[Authorize]
public class PassportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    public PassportController(IMediator mediator, ICurrentUserService currentUserService, IAuditService auditService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    [HttpGet("api/v1/passports/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPassport(Guid id)
    {
        var result = await _mediator.Send(new GetBusinessPassportQuery(id));
        if (result.IsSuccess)
        {
            var orgId = _currentUserService.OrganizationId;
            var userId = _currentUserService.UserId;

            if (orgId.HasValue && userId.HasValue)
            {
                await _auditService.RecordAsync(new RecordAuditOptions(
                    Action: "PASSPORT_VIEWED",
                    Category: "PASSPORT",
                    TargetType: "BUSINESS_PASSPORT",
                    TargetId: result.Value.PassportNumber ?? id.ToString(),
                    TargetName: result.Value.BusinessName ?? "Sovereign Passport Dossier",
                    Result: "SUCCESS",
                    OverrideOrganizationId: orgId.Value,
                    OverrideUserId: userId.Value
                ));
            }
        }

        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("api/v1/passports/{businessId}/generate")]
    public async Task<IActionResult> GeneratePassport(Guid businessId)
    {
        var result = await _mediator.Send(new GeneratePassportCommand(businessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("api/v1/businesses/{businessId}/passport/publish")]
    public async Task<IActionResult> PublishPassport(Guid businessId)
    {
        var result = await _mediator.Send(new PublishPassportCommand(businessId));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("api/v1/businesses/{businessId}/passport/unpublish")]
    public async Task<IActionResult> UnpublishPassport(Guid businessId)
    {
        var result = await _mediator.Send(new UnpublishPassportCommand(businessId));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("api/v1/passports/{passportId}/share")]
    public async Task<IActionResult> CreateShare(Guid passportId, [FromBody] CreateShareRequest request)
    {
        var command = new CreatePassportShareCommand(
            passportId,
            request.RecipientEmail,
            request.RecipientName,
            request.AccessScope ?? new Dictionary<string, bool> { ["gst"] = true, ["pan"] = true },
            request.ExpiryDays ?? 30,
            _currentUserService.UserId ?? Guid.Empty
        );

        var result = await _mediator.Send(command);
        if (result.IsSuccess)
        {
            await _auditService.RecordAsync(new RecordAuditOptions(
                Action: "PASSPORT_SHARED",
                Category: "PASSPORT",
                TargetType: "BUSINESS_PASSPORT",
                TargetId: passportId.ToString(),
                TargetName: $"Passport Shared with {request.RecipientName ?? request.RecipientEmail ?? "Recipient"}",
                Result: "SUCCESS",
                OverrideOrganizationId: _currentUserService.OrganizationId,
                OverrideUserId: _currentUserService.UserId
            ));
        }
        return result.IsSuccess ? Ok(new { shareUrl = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("api/v1/passports/{passportId}/shares")]
    public async Task<IActionResult> GetShares(Guid passportId)
    {
        var result = await _mediator.Send(new GetPassportSharesQuery(passportId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("api/v1/passports/{passportId}/shares/{shareId}")]
    public async Task<IActionResult> DeleteShare(Guid passportId, Guid shareId)
    {
        var result = await _mediator.Send(new DeletePassportShareCommand(passportId, shareId));
        if (result.IsSuccess)
        {
            await _auditService.RecordAsync(new RecordAuditOptions(
                Action: "PASSPORT_ACCESS_REVOKED",
                Category: "SECURITY",
                TargetType: "BUSINESS_PASSPORT",
                TargetId: passportId.ToString(),
                TargetName: $"Revoked Passport Share ({shareId})",
                Result: "SUCCESS",
                OverrideOrganizationId: _currentUserService.OrganizationId,
                OverrideUserId: _currentUserService.UserId
            ));
        }
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    public record CreateShareRequest(string? RecipientEmail, string? RecipientName, Dictionary<string, bool>? AccessScope, int? ExpiryDays);
}
