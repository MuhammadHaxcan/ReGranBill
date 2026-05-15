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
    private readonly IDownstreamUsageService _downstreamService;

    public WashingVouchersController(IWashingVoucherService service, IPdfService pdfService, IDownstreamUsageService downstreamService)
    {
        _service = service;
        _pdfService = pdfService;
        _downstreamService = downstreamService;
    }

    [HttpGet("{id}/downstream")]
    public async Task<IActionResult> GetDownstreamUsage(int id)
    {
        var voucher = await _service.GetByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(await _downstreamService.GetForWashingAsync(id));
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWashingVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
            return this.InvalidUserSession();

        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateWashingVoucherRequest request)
    {
        var existing = await _service.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var result = await _service.UpdateAsync(id, request);
        return Ok(result);
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
