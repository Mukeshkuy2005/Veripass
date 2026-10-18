using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.TrustScore;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/buyers/{buyerId}")]
[Route("api/buyers/{buyerId}")]
[Authorize]
public class BuyerTrustScoresController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public BuyerTrustScoresController(
        IMediator mediator,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    private Guid ResolveBuyerId(string buyerId)
    {
        if (Guid.TryParse(buyerId, out var parsed))
        {
            return parsed;
        }

        if (buyerId.Equals("me", StringComparison.OrdinalIgnoreCase) && _currentUserService.OrganizationId.HasValue)
        {
            return _currentUserService.OrganizationId.Value;
        }

        return _currentUserService.OrganizationId ?? Guid.Empty;
    }

    /// <summary>
    /// Gets the active weighted verification profile for a buyer.
    /// </summary>
    [HttpGet("scoring-profile")]
    public async Task<IActionResult> GetScoringProfile([FromRoute] string buyerId)
    {
        var resolvedBuyerId = ResolveBuyerId(buyerId);
        if (resolvedBuyerId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid buyer organization identifier." });
        }

        var result = await _mediator.Send(new GetBuyerVerificationProfileQuery(resolvedBuyerId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Updates the active weighted verification profile for a buyer.
    /// Enforces strict validation: weights must sum to exactly 100.
    /// Recalculates all affected supplier pairs upon saving.
    /// </summary>
    [HttpPut("scoring-profile")]
    public async Task<IActionResult> UpdateScoringProfile(
        [FromRoute] string buyerId,
        [FromBody] UpdateProfileRequest request)
    {
        var resolvedBuyerId = ResolveBuyerId(buyerId);
        if (resolvedBuyerId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid buyer organization identifier." });
        }

        // Server-Side Validation
        if (request == null || request.Requirements == null || request.Requirements.Count == 0)
        {
            return BadRequest(new { message = "At least one credential requirement must be specified." });
        }

        int totalWeight = request.Requirements.Sum(r => r.Weight);
        if (totalWeight != 100)
        {
            return BadRequest(new { message = $"Buyer profile weights must sum to exactly 100. Current sum is {totalWeight}." });
        }

        foreach (var req in request.Requirements)
        {
            if (string.IsNullOrWhiteSpace(req.Type))
            {
                return BadRequest(new { message = "Credential requirement type cannot be empty." });
            }

            if (req.Weight <= 0 || req.Weight > 100)
            {
                return BadRequest(new { message = $"Weight for requirement '{req.Type}' must be between 1 and 100." });
            }
        }

        var command = new UpdateBuyerVerificationProfileCommand(
            BuyerOrganizationId: resolvedBuyerId,
            Name: string.IsNullOrWhiteSpace(request.Name) ? "Custom Buyer Profile" : request.Name.Trim(),
            Description: request.Description,
            Requirements: request.Requirements.Select(r => new RequirementConfigDto(
                Type: r.Type.Trim().ToUpperInvariant(),
                IsRequired: r.IsRequired,
                Weight: r.Weight,
                Rules: r.Rules,
                CredentialTypeId: r.CredentialTypeId
            )).ToList()
        );

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Gets the authoritative buyer-specific trust score and breakdown for a supplier.
    /// </summary>
    [HttpGet("suppliers/{supplierId:guid}/trust-score")]
    public async Task<IActionResult> GetSupplierTrustScore(
        [FromRoute] string buyerId,
        [FromRoute] Guid supplierId)
    {
        var resolvedBuyerId = ResolveBuyerId(buyerId);
        if (resolvedBuyerId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid buyer organization identifier." });
        }

        var result = await _mediator.Send(new GetBuyerTrustScoreQuery(resolvedBuyerId, supplierId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    /// <summary>
    /// Gets batch buyer-specific trust scores for a collection of supplier IDs.
    /// </summary>
    [HttpPost("trust-scores/batch")]
    public async Task<IActionResult> GetBatchTrustScores(
        [FromRoute] string buyerId,
        [FromBody] BatchTrustScoreRequest request)
    {
        var resolvedBuyerId = ResolveBuyerId(buyerId);
        if (resolvedBuyerId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid buyer organization identifier." });
        }

        if (request == null || request.SupplierIds == null || request.SupplierIds.Count == 0)
        {
            return BadRequest(new { message = "At least one supplier ID must be provided." });
        }

        if (request.SupplierIds.Count > 100)
        {
            return BadRequest(new { message = "Cannot request more than 100 supplier trust scores in a single batch." });
        }

        var result = await _mediator.Send(new GetBatchBuyerTrustScoresQuery(resolvedBuyerId, request.SupplierIds));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Gets the historical trust score audit snapshots for a buyer-supplier pair.
    /// </summary>
    [HttpGet("suppliers/{supplierId:guid}/trust-score/history")]
    public async Task<IActionResult> GetTrustScoreHistory(
        [FromRoute] string buyerId,
        [FromRoute] Guid supplierId)
    {
        var resolvedBuyerId = ResolveBuyerId(buyerId);
        if (resolvedBuyerId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid buyer organization identifier." });
        }

        var result = await _mediator.Send(new GetBuyerTrustScoreHistoryQuery(resolvedBuyerId, supplierId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }
}

public class UpdateProfileRequest
{
    [Required]
    public string Name { get; set; } = "Standard Verification Profile";

    public string? Description { get; set; }

    [Required]
    public List<RequirementConfigRequest> Requirements { get; set; } = new();
}

public class RequirementConfigRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    [Range(1, 100)]
    public int Weight { get; set; }

    public string? Rules { get; set; }

    public Guid? CredentialTypeId { get; set; }
}

public class BatchTrustScoreRequest
{
    [Required]
    public List<Guid> SupplierIds { get; set; } = new();
}
