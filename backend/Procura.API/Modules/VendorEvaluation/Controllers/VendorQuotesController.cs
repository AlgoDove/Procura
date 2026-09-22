using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;

namespace Procura.API.Modules.VendorEvaluation.Controllers;

[ApiController]
[Route("api/vendor-quotes")]
[Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER")]
public class VendorQuotesController : ControllerBase
{
    private readonly IVendorQuoteService _quoteService;

    public VendorQuotesController(IVendorQuoteService quoteService)
    {
        _quoteService = quoteService;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitQuote([FromBody] CreateVendorQuoteDto dto, CancellationToken cancellationToken)
    {
        var quote = await _quoteService.SubmitQuoteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetQuoteById), new { id = quote.Id }, quote);
    }

    [HttpGet("procurement-request/{requestId}")]
    public async Task<IActionResult> GetQuotesByRequestId(Guid requestId, CancellationToken cancellationToken)
    {
        var quotes = await _quoteService.GetQuotesByRequestIdAsync(requestId, cancellationToken);
        return Ok(quotes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuoteById(Guid id, CancellationToken cancellationToken)
    {
        var quote = await _quoteService.GetQuoteByIdAsync(id, cancellationToken);
        return quote == null ? NotFound() : Ok(quote);
    }
}