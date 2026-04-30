using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.SaleReturns;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/sale-returns")]
[Authorize]
public class SaleReturnsController : ControllerBase
{
    private readonly ISaleReturnService _srService;
    private readonly IPdfService _pdfService;

    public SaleReturnsController(ISaleReturnService srService, IPdfService pdfService)
    {
        _srService = srService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _srService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sr = await _srService.GetByIdAsync(id);
        if (sr == null) return NotFound();
        return Ok(sr);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var srNumber = await _srService.GetNextNumberAsync();
        return Ok(new { srNumber });
    }

    [HttpGet("latest-rates")]
    public async Task<IActionResult> GetLatestRates([FromQuery] string? productIds)
    {
        var ids = ParseProductIds(Request.Query["productIds"], productIds);
        return Ok(await _srService.GetLatestRatesAsync(ids));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleReturnRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _srService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateSaleReturnRequest request)
    {
        var existing = await _srService.GetByIdAsync(id);
        if (existing == null) return NotFound();
        if (existing.RatesAdded && !User.IsInRole("Admin")) return Forbid();

        var result = await _srService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var sr = await _srService.GetByIdAsync(id);
        if (sr == null) return NotFound();

        var pdfBytes = _pdfService.GenerateSaleReturnPdf(sr);
        return File(pdfBytes, "application/pdf", $"SaleReturn-{sr.SrNumber}.pdf");
    }

    [HttpPatch("{id}/rates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdateSaleReturnRatesRequest request)
    {
        if (!await _srService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _srService.GetByIdAsync(id));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _srService.SoftDeleteAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }

    private static IReadOnlyCollection<int> ParseProductIds(StringValues values, string? productIds)
    {
        IEnumerable<string> rawValues = values;
        if (!string.IsNullOrWhiteSpace(productIds))
            rawValues = rawValues.Append(productIds);

        return rawValues
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => int.TryParse(v, out var parsed) ? parsed : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToArray();
    }
}
