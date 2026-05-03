using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.CashVouchers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/cash-vouchers")]
[Authorize]
public class CashVouchersController : ControllerBase
{
    private readonly ICashVoucherService _cashVoucherService;

    public CashVouchersController(ICashVoucherService cashVoucherService) =>
        _cashVoucherService = cashVoucherService;

    [HttpGet("receipt/next-number")]
    [RequirePage("receipt-voucher")]
    public async Task<IActionResult> GetNextReceiptNumber()
    {
        var voucherNumber = await _cashVoucherService.GetNextReceiptNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpGet("payment/next-number")]
    [RequirePage("payment-voucher")]
    public async Task<IActionResult> GetNextPaymentNumber()
    {
        var voucherNumber = await _cashVoucherService.GetNextPaymentNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpGet("receipt/{id}")]
    [RequirePage("receipt-voucher")]
    public async Task<IActionResult> GetReceiptById(int id)
    {
        var voucher = await _cashVoucherService.GetReceiptByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(voucher);
    }

    [HttpGet("payment/{id}")]
    [RequirePage("payment-voucher")]
    public async Task<IActionResult> GetPaymentById(int id)
    {
        var voucher = await _cashVoucherService.GetPaymentByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(voucher);
    }

    [HttpPost("receipt")]
    [RequirePage("receipt-voucher")]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateCashVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _cashVoucherService.CreateReceiptAsync(request, userId);
        return CreatedAtAction(nameof(GetReceiptById), new { id = result.Id }, result);
    }

    [HttpPost("payment")]
    [RequirePage("payment-voucher")]
    public async Task<IActionResult> CreatePayment([FromBody] CreateCashVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
        {
            return this.InvalidUserSession();
        }

        var result = await _cashVoucherService.CreatePaymentAsync(request, userId);
        return CreatedAtAction(nameof(GetPaymentById), new { id = result.Id }, result);
    }

    [HttpPut("receipt/{id}")]
    [RequirePage("receipt-voucher")]
    public async Task<IActionResult> UpdateReceipt(int id, [FromBody] CreateCashVoucherRequest request)
    {
        var result = await _cashVoucherService.UpdateReceiptAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("payment/{id}")]
    [RequirePage("payment-voucher")]
    public async Task<IActionResult> UpdatePayment(int id, [FromBody] CreateCashVoucherRequest request)
    {
        var result = await _cashVoucherService.UpdatePaymentAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
