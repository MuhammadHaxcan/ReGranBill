using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.Helpers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/delivery-challans")]
[Authorize]
[RequirePage("delivery-challan")]
public class DeliveryChallansController : ControllerBase
{
    private readonly IDeliveryChallanService _dcService;
    private readonly IPdfService _pdfService;

    public DeliveryChallansController(IDeliveryChallanService dcService, IPdfService pdfService)
    {
        _dcService = dcService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _dcService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dc = await _dcService.GetByIdAsync(id);
        if (dc == null) return NotFound();
        return Ok(dc);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var dcNumber = await _dcService.GetNextNumberAsync();
        return Ok(new { dcNumber });
    }

    [HttpGet("latest-rates")]
    public async Task<IActionResult> GetLatestRates([FromQuery] string? productIds)
    {
        var ids = ParseProductIds(Request.Query["productIds"], productIds);
        return Ok(await _dcService.GetLatestRatesAsync(ids));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDcRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _dcService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDcRequest request)
    {
        var existing = await _dcService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var result = await _dcService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var dc = await _dcService.GetByIdAsync(id);
        if (dc == null) return NotFound();

        var pdfBytes = _pdfService.GenerateDeliveryChallanPdf(dc);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(dc.CustomerName, dc.DcNumber, dc.Date));
    }

    [HttpGet("by-number/{dcNumber}/pdf")]
    public async Task<IActionResult> GetPdfByNumber(string dcNumber)
    {
        var dc = await _dcService.GetByNumberAsync(dcNumber);
        if (dc == null) return NotFound();

        var pdfBytes = _pdfService.GenerateDeliveryChallanPdf(dc);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(dc.CustomerName, dc.DcNumber, dc.Date));
    }

    [HttpPatch("{id}/rates")]
    [RequirePage("voucher-rates")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdateDcRatesRequest request)
    {
        if (!await _dcService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _dcService.GetByIdAsync(id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _dcService.SoftDeleteAsync(id);
        if (error != null) return Conflict(new { statusCode = StatusCodes.Status409Conflict, message = error });
        if (!success) return NotFound();
        return NoContent();
    }

    private static IReadOnlyCollection<int> ParseProductIds(StringValues values, string? productIds)
    {
        IEnumerable<string> rawValues = values;
        if (!string.IsNullOrWhiteSpace(productIds))
        {
            rawValues = rawValues.Append(productIds);
        }

        return rawValues
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => int.TryParse(v, out var parsed) ? parsed : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToArray();
    }
}
