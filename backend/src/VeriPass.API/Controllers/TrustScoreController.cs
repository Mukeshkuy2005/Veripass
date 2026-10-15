using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.TrustScore;
using VeriPass.Application.Verification;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/trust-score")]
[Authorize]
public class TrustScoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public TrustScoreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTrustScore(Guid id)
    {
        var result = await _mediator.Send(new VeriPass.Application.Verification.GetTrustScoreQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{businessId:guid}/history")]
    public async Task<IActionResult> GetTrustScoreHistory(Guid businessId)
    {
        var result = await _mediator.Send(new GetTrustScoreHistoryQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("{id:guid}/recalculate")]
    public async Task<IActionResult> Recalculate(Guid id)
    {
        var result = await _mediator.Send(new RecalculateTrustScoreCommand(id));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }
}
