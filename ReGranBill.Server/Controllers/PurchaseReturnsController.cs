using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.Helpers;
using ReGranBill.Server.DTOs.PurchaseReturns;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/purchase-returns")]
[Authorize]
[RequirePage("purchase-return")]
public class PurchaseReturnsController : ControllerBase
{
    private readonly IPurchaseReturnService _prService;
    private readonly IPdfService _pdfService;
    private readonly IDownstreamUsageService _downstreamService;

    public PurchaseReturnsController(IPurchaseReturnService prService, IPdfService pdfService, IDownstreamUsageService downstreamService)
    {
        _prService = prService;
        _pdfService = pdfService;
        _downstreamService = downstreamService;
    }

    [HttpGet("{id}/downstream")]
    public async Task<IActionResult> GetDownstreamUsage(int id)
    {
        var pr = await _prService.GetByIdAsync(id);
        if (pr == null) return NotFound();
        return Ok(await _downstreamService.GetForPurchaseReturnAsync(id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _prService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pr = await _prService.GetByIdAsync(id);
        if (pr == null) return NotFound();
        return Ok(pr);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var prNumber = await _prService.GetNextNumberAsync();
        return Ok(new { prNumber });
    }

    [HttpGet("latest-rates")]
    public async Task<IActionResult> GetLatestRates([FromQuery] string? productIds)
    {
        var ids = ParseProductIds(Request.Query["productIds"], productIds);
        return Ok(await _prService.GetLatestRatesAsync(ids));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseReturnRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _prService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePurchaseReturnRequest request)
    {
        var existing = await _prService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var result = await _prService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var pr = await _prService.GetByIdAsync(id);
        if (pr == null) return NotFound();

        var pdfBytes = _pdfService.GeneratePurchaseReturnPdf(pr);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(pr.VendorName, pr.PrNumber, pr.Date));
    }

    [HttpGet("by-number/{prNumber}/pdf")]
    public async Task<IActionResult> GetPdfByNumber(string prNumber)
    {
        var pr = await _prService.GetByNumberAsync(prNumber);
        if (pr == null) return NotFound();

        var pdfBytes = _pdfService.GeneratePurchaseReturnPdf(pr);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(pr.VendorName, pr.PrNumber, pr.Date));
    }

    [HttpPatch("{id}/rates")]
    [RequirePage("voucher-rates")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdatePurchaseReturnRatesRequest request)
    {
        if (!await _prService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _prService.GetByIdAsync(id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _prService.SoftDeleteAsync(id);
        if (error != null) return Conflict(new { statusCode = StatusCodes.Status409Conflict, message = error });
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
