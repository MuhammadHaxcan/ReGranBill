using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/sale-purchase-report")]
[Authorize(Roles = "Admin")]
public class SalePurchaseReportController : ControllerBase
{
    private readonly ISalePurchaseReportService _reportService;

    public SalePurchaseReportController(ISalePurchaseReportService reportService) => _reportService = reportService;

    [HttpGet]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? type,
        [FromQuery] int? productId)
    {
        var result = await _reportService.GetReportAsync(from, to, type, productId);
        return Ok(result);
    }
}
