using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/inventory-lots")]
[Authorize]
public class InventoryLotsController : ControllerBase
{
    private readonly IInventoryLotService _service;

    public InventoryLotsController(IInventoryLotService service) => _service = service;

    [HttpGet("available-for-washing")]
    public async Task<IActionResult> GetAvailableForWashing([FromQuery] int vendorId, [FromQuery] int accountId, [FromQuery] int? voucherId) =>
        Ok(await _service.GetAvailableLotsForWashingAsync(vendorId, accountId, voucherId));

    [HttpGet("available-for-production")]
    public async Task<IActionResult> GetAvailableForProduction([FromQuery] int accountId, [FromQuery] int? voucherId) =>
        Ok(await _service.GetAvailableLotsForProductionAsync(accountId, voucherId));

    [HttpGet("available-for-purchase-return")]
    public async Task<IActionResult> GetAvailableForPurchaseReturn([FromQuery] int vendorId, [FromQuery] int accountId, [FromQuery] int? voucherId) =>
        Ok(await _service.GetAvailableLotsForPurchaseReturnAsync(vendorId, accountId, voucherId));
}
