using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Relationships;
using VeriPass.Application.Suppliers;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public SuppliersController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? query,
        [FromQuery] string? industry,
        [FromQuery] string? state,
        [FromQuery] int? minTrustScore,
        [FromQuery] string? credential,
        [FromQuery] string? verificationStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? buyerOnly = null)
    {
        var isBuyerOnly = buyerOnly ?? (_currentUserService.IsAuthenticated && _currentUserService.Roles.Contains("Buyer"));
        var result = await _mediator.Send(new SearchSuppliersQuery(query, state, industry, minTrustScore, credential, verificationStatus, page, pageSize, isBuyerOnly));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{businessId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSupplier(Guid businessId)
    {
        var result = await _mediator.Send(new GetSupplierByIdQuery(businessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? industry,
        [FromQuery] string? state,
        [FromQuery] int? minTrustScore,
        [FromQuery] string? requiredCredential,
        [FromQuery] string? verificationStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? buyerOnly = null)
    {
        var isBuyerOnly = buyerOnly ?? (_currentUserService.IsAuthenticated && _currentUserService.Roles.Contains("Buyer"));
        var result = await _mediator.Send(new SearchSuppliersQuery(query, state, industry, minTrustScore, requiredCredential, verificationStatus, page, pageSize, isBuyerOnly));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // GET /api/v1/suppliers/compare?businessIds=id1,id2
    [HttpGet("compare")]
    [AllowAnonymous]
    public async Task<IActionResult> CompareGet([FromQuery] string businessIds)
    {
        var ids = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(businessIds))
        {
            foreach (var part in businessIds.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParse(part.Trim(), out var g)) ids.Add(g);
            }
        }
        var result = await _mediator.Send(new CompareSuppliersQuery(ids));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPost("compare")]
    [AllowAnonymous]
    public async Task<IActionResult> Compare([FromBody] List<Guid> businessIds)
    {
        var result = await _mediator.Send(new CompareSuppliersQuery(businessIds));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }
}
