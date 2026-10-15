using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Relationships;
using VeriPass.Domain.Enums;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RelationshipsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RelationshipsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetRelationships([FromQuery] bool isBuyer = true, [FromQuery] Guid? organizationId = null)
    {
        var orgId = organizationId ?? _currentUserService.OrganizationId;
        if (!orgId.HasValue) return Ok(new List<object>());
        var result = await _mediator.Send(new GetRelationshipsQuery(orgId.Value, isBuyer));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("invite")]
    [AllowAnonymous]
    public async Task<IActionResult> Invite([FromBody] InviteSupplierRequest request)
    {
        var buyerOrgId = request.BuyerOrganizationId ?? _currentUserService.OrganizationId;
        if (!buyerOrgId.HasValue) return BadRequest(new { message = "Buyer organization required." });
        var command = new InviteSupplierCommand(buyerOrgId.Value, request.Email ?? "vendor@supplier.in", request.Phone, request.SupplierBusinessId);
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { invitationId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRelationshipRequest request)
    {
        RelationshipStatus status;
        if (Enum.TryParse<RelationshipStatus>(request.Status, true, out var parsed))
        {
            status = parsed;
        }
        else if (request.Status?.Equals("IN_VERIFICATION", StringComparison.OrdinalIgnoreCase) == true)
        {
            status = RelationshipStatus.SUSPENDED; // Maps to IN_VERIFICATION in system
        }
        else if (request.Status?.Equals("IDENTIFIED", StringComparison.OrdinalIgnoreCase) == true)
        {
            status = RelationshipStatus.INVITED;
        }
        else if (request.Status?.Equals("CONTACTED", StringComparison.OrdinalIgnoreCase) == true)
        {
            status = RelationshipStatus.PENDING;
        }
        else
        {
            status = RelationshipStatus.PENDING;
        }

        var result = await _mediator.Send(new UpdateRelationshipStatusCommand(id, status));
        return result.IsSuccess ? Ok(new { success = true, status = status.ToString() }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/stage")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStage(Guid id, [FromBody] UpdateStageRequest request)
    {
        var status = request.Stage?.ToUpperInvariant() switch
        {
            "IDENTIFIED" => RelationshipStatus.INVITED,
            "CONTACTED" => RelationshipStatus.PENDING,
            "IN_VERIFICATION" => RelationshipStatus.SUSPENDED,
            "APPROVED" => RelationshipStatus.APPROVED,
            "REJECTED" => RelationshipStatus.REJECTED,
            _ => RelationshipStatus.PENDING
        };

        var result = await _mediator.Send(new UpdateRelationshipStatusCommand(id, status));
        return result.IsSuccess ? Ok(new { success = true, stage = request.Stage }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/approve")]
    [AllowAnonymous]
    public async Task<IActionResult> ApproveRelationship(Guid id)
    {
        var result = await _mediator.Send(new UpdateRelationshipStatusCommand(id, RelationshipStatus.APPROVED));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/reject")]
    [AllowAnonymous]
    public async Task<IActionResult> RejectRelationship(Guid id)
    {
        var result = await _mediator.Send(new UpdateRelationshipStatusCommand(id, RelationshipStatus.REJECTED));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/suspend")]
    [AllowAnonymous]
    public async Task<IActionResult> SuspendRelationship(Guid id)
    {
        var result = await _mediator.Send(new UpdateRelationshipStatusCommand(id, RelationshipStatus.SUSPENDED));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/auto-qualify")]
    [AllowAnonymous]
    public async Task<IActionResult> AutoQualify(Guid id)
    {
        var result = await _mediator.Send(new AutoQualifyRelationshipCommand(id));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("batch-auto-qualify")]
    [AllowAnonymous]
    public async Task<IActionResult> BatchAutoQualify([FromBody] BatchAutoQualifyRequest? request)
    {
        var buyerId = request?.BuyerOrganizationId ?? _currentUserService.OrganizationId;
        if (!buyerId.HasValue) return BadRequest(new { message = "Buyer organization required." });
        var minScore = request?.MinTrustScore ?? 80;
        var result = await _mediator.Send(new BatchAutoQualifyCommand(buyerId.Value, minScore));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("automation-policy")]
    [AllowAnonymous]
    public IActionResult GetAutomationPolicy()
    {
        return Ok(new
        {
            autoPilotEnabled = true,
            minTrustScore = 85,
            autoGenerateErpCode = true,
            requireGstnMatch = true,
            requireCbdtPanMatch = true,
            requireBankPennyDrop = true,
            requireIsoAccreditation = false,
            stpExecutionMode = "AUTONOMOUS_STRAIGHT_THROUGH",
            policyName = "Enterprise Tier-1 Direct Procurement Policy",
            targetSlaMinutes = 5,
            averageCycleTime = "4.2 mins",
            autonomousEmpanelmentRate = 83.3,
            lastAuditSync = DateTime.UtcNow.AddMinutes(-12)
        });
    }

    public record UpdateRelationshipRequest(string Status);
    public record UpdateStageRequest(string Stage);
    public record InviteSupplierRequest(Guid? BuyerOrganizationId, string? Email, string? Phone, Guid? SupplierBusinessId);
    public record BatchAutoQualifyRequest(Guid? BuyerOrganizationId, int? MinTrustScore);
}
