using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Passport;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("passports/{identifier}")]
    public async Task<IActionResult> GetPublicPassport(string identifier, [FromQuery] string? token = null)
    {
        var result = await _mediator.Send(new GetPublicPassportQuery(identifier, token));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("passports/{identifier}/verify")]
    public async Task<IActionResult> VerifyPublicPassport(string identifier)
    {
        var result = await _mediator.Send(new VerifyPublicPassportQuery(identifier));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("passports/{identifier}/qr")]
    public IActionResult GetQrPlaceholder(string identifier)
    {
        var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200' width='200' height='200'>
            <rect width='200' height='200' fill='white'/>
            <rect x='20' y='20' width='50' height='50' fill='#0B192C'/>
            <rect x='30' y='30' width='30' height='30' fill='white'/>
            <rect x='37' y='37' width='16' height='16' fill='#0B192C'/>
            <rect x='130' y='20' width='50' height='50' fill='#0B192C'/>
            <rect x='140' y='30' width='30' height='30' fill='white'/>
            <rect x='147' y='37' width='16' height='16' fill='#0B192C'/>
            <rect x='20' y='130' width='50' height='50' fill='#0B192C'/>
            <rect x='30' y='140' width='30' height='30' fill='white'/>
            <rect x='37' y='147' width='16' height='16' fill='#0B192C'/>
            <rect x='90' y='20' width='20' height='20' fill='#0B192C'/>
            <rect x='90' y='60' width='20' height='40' fill='#0B192C'/>
            <rect x='130' y='90' width='30' height='20' fill='#0B192C'/>
            <rect x='90' y='130' width='40' height='20' fill='#0B192C'/>
            <rect x='150' y='140' width='30' height='40' fill='#0B192C'/>
            <text x='100' y='185' font-family='monospace' font-size='9' text-anchor='middle' fill='#64748B'>{identifier}</text>
        </svg>";

        return Content(svg, "image/svg+xml");
    }
}
