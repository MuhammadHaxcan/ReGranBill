using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.JournalVouchers;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/journal-vouchers")]
[Authorize(Roles = "Admin,Operator")]
public class JournalVouchersController : ControllerBase
{
    private readonly IJournalVoucherService _journalVoucherService;

    public JournalVouchersController(IJournalVoucherService journalVoucherService)
    {
        _journalVoucherService = journalVoucherService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _journalVoucherService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var voucher = await _journalVoucherService.GetByIdAsync(id);
        if (voucher == null) return NotFound();
        return Ok(voucher);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var voucherNumber = await _journalVoucherService.GetNextNumberAsync();
        return Ok(new { voucherNumber });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJournalVoucherRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _journalVoucherService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateJournalVoucherRequest request)
    {
        try
        {
            var result = await _journalVoucherService.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
