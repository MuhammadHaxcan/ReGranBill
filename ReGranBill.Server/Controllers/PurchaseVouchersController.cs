using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/purchase-vouchers")]
[Authorize]
public class PurchaseVouchersController : ControllerBase
{
    private readonly IPurchaseVoucherService _purchaseService;
    private readonly IPdfService _pdfService;

    public PurchaseVouchersController(IPurchaseVoucherService purchaseService, IPdfService pdfService)
    {
        _purchaseService = purchaseService;
        _pdfService = pdfService;
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseVoucherRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _purchaseService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePurchaseVoucherRequest request)
    {
        var result = await _purchaseService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var voucher = await _purchaseService.GetByIdAsync(id);
        if (voucher == null) return NotFound();

        var pdfBytes = _pdfService.GeneratePurchaseVoucherPdf(voucher);
        return File(pdfBytes, "application/pdf", $"PurchaseVoucher-{voucher.VoucherNumber}.pdf");
    }

    [HttpPatch("{id}/rates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdatePurchaseVoucherRatesRequest request)
    {
        if (!await _purchaseService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _purchaseService.GetByIdAsync(id));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _purchaseService.SoftDeleteAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}
