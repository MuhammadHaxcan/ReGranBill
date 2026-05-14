using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.WashingVouchers;
using ReGranBill.Server.Helpers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/washing-vouchers")]
[Authorize]
[RequirePage("washing-room")]
public class WashingVouchersController : ControllerBase
{
    private readonly IWashingVoucherService _service;
    private readonly IPdfService _pdfService;

    public WashingVouchersController(IWashingVoucherService service, IPdfService pdfService)
    {
        _service = service;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var voucher = await _service.GetByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(voucher);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var voucher = await _service.GetByIdAsync(id);
        if (voucher == null) return NotFound();

        var pdfBytes = _pdfService.GenerateWashingVoucherPdf(voucher);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(voucher.SourceVendorName, voucher.VoucherNumber, voucher.Date));
    }

    [HttpGet("by-number/{voucherNumber}/pdf")]
    public async Task<IActionResult> GetPdfByNumber(string voucherNumber)
    {
        var voucher = await _service.GetByNumberAsync(voucherNumber);
        if (voucher == null) return NotFound();

        var pdfBytes = _pdfService.GenerateWashingVoucherPdf(voucher);
        return File(pdfBytes, "application/pdf", PdfFileNameHelper.BuildVoucherFileName(voucher.SourceVendorName, voucher.VoucherNumber, voucher.Date));
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var voucherNumber = await _service.GetNextNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpGet("latest-unwashed-rate")]
    public async Task<IActionResult> GetLatestUnwashedRate([FromQuery] int vendorId, [FromQuery] int accountId)
    {
        var rate = await _service.GetLatestUnwashedRateAsync(vendorId, accountId);
        if (rate == null) return Ok(new { rate = (decimal?)null });
        return Ok(rate);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWashingVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
            return this.InvalidUserSession();

        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _service.SoftDeleteAsync(id);
        if (error != null) return Conflict(new { statusCode = StatusCodes.Status409Conflict, message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}
