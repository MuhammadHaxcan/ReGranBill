using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.ProductionVouchers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/production-vouchers")]
[Authorize]
[RequirePage("production-voucher")]
public class ProductionVouchersController : ControllerBase
{
    private readonly IProductionVoucherService _service;

    public ProductionVouchersController(IProductionVoucherService service)
    {
        _service = service;
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

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var voucherNumber = await _service.GetNextNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpGet("latest-purchase-rates")]
    public async Task<IActionResult> GetLatestPurchaseRates([FromQuery] int vendorId, [FromQuery] string accountIds)
    {
        var ids = (accountIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        var rates = await _service.GetLatestPurchaseRatesAsync(vendorId, ids);
        return Ok(rates);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductionVoucherRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
            return this.InvalidUserSession();

        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateProductionVoucherRequest request)
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
