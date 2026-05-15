using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.Helpers;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/purchase-vouchers")]
[Authorize]
[RequirePage("purchase-voucher")]
public class PurchaseVouchersController : ControllerBase
{
    private readonly IPurchaseVoucherService _purchaseService;
    private readonly IPdfService _pdfService;
    private readonly IDownstreamUsageService _downstreamService;

    public PurchaseVouchersController(IPurchaseVoucherService purchaseService, IPdfService pdfService, IDownstreamUsageService downstreamService)
    {
        _purchaseService = purchaseService;
        _pdfService = pdfService;
        _downstreamService = downstreamService;
    }

    [HttpGet("{id}/downstream")]
    public async Task<IActionResult> GetDownstreamUsage(int id)
    {
        var voucher = await _purchaseService.GetByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(await _downstreamService.GetForPurchaseAsync(id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _purchaseService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var voucher = await _purchaseService.GetByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(voucher);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var voucherNumber = await _purchaseService.GetNextNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpGet("latest-rates")]
    public async Task<IActionResult> GetLatestRates([FromQuery] string? productIds)
    {
        var ids = ParseProductIds(Request.Query["productIds"], productIds);
        return Ok(await _purchaseService.GetLatestRatesAsync(ids));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _purchaseService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePurchaseVoucherRequest request)
    {
        var existing = await _purchaseService.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var result = await _purchaseService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var voucher = await _purchaseService.GetByIdAsync(id);
        if (voucher == null) return NotFound();

        var pdfBytes = _pdfService.GeneratePurchaseVoucherPdf(voucher);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(voucher.VendorName, voucher.VoucherNumber, voucher.Date));
    }

    [HttpGet("by-number/{voucherNumber}/pdf")]
    public async Task<IActionResult> GetPdfByNumber(string voucherNumber)
    {
        var voucher = await _purchaseService.GetByNumberAsync(voucherNumber);
        if (voucher == null) return NotFound();

        var pdfBytes = _pdfService.GeneratePurchaseVoucherPdf(voucher);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(voucher.VendorName, voucher.VoucherNumber, voucher.Date));
    }

    [HttpPatch("{id}/rates")]
    [RequirePage("voucher-rates")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdatePurchaseVoucherRatesRequest request)
    {
        if (!await _purchaseService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _purchaseService.GetByIdAsync(id));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _purchaseService.SoftDeleteAsync(id);
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
