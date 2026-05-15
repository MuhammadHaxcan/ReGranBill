using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.Inventory;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/raw-material-lot-report")]
[Authorize]
[RequirePage("raw-material-lot-report")]
public class RawMaterialLotReportController : ControllerBase
{
    private readonly IInventoryLotService _service;

    public RawMaterialLotReportController(IInventoryLotService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] RawMaterialLotReportQueryDto query) =>
        Ok(await _service.GetRawMaterialLotReportAsync(query));
}
