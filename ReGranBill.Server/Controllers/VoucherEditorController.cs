using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.VoucherEditor;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/voucher-editor")]
[Authorize(Roles = "Admin,Operator")]
public class VoucherEditorController : ControllerBase
{
    private readonly IVoucherEditorService _voucherEditorService;

    public VoucherEditorController(IVoucherEditorService voucherEditorService)
    {
        _voucherEditorService = voucherEditorService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string voucherType,
        [FromQuery] string voucherNumber)
    {
        try
        {
            var voucher = await _voucherEditorService.FindByTypeAndNumberAsync(voucherType, voucherNumber);
            if (voucher == null) return NotFound();
            return Ok(voucher);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateVoucherLedgerRequest request)
    {
        try
        {
            var voucher = await _voucherEditorService.UpdateAsync(request);
            if (voucher == null) return NotFound();
            return Ok(voucher);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
