using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/master-report")]
[Authorize(Roles = "Admin")]
public class MasterReportController : ControllerBase
{
    private readonly IMasterReportService _reportService;

    public MasterReportController(IMasterReportService reportService) => _reportService = reportService;

    [HttpGet]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? categoryId,
        [FromQuery] int? accountId)
    {
        var result = await _reportService.GetReportAsync(from, to, categoryId, accountId);
        return Ok(result);
    }
}
