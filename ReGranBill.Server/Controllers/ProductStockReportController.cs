using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/product-stock-report")]
[Authorize(Roles = "Admin")]
public class ProductStockReportController : ControllerBase
{
    private readonly IProductStockReportService _reportService;

    public ProductStockReportController(IProductStockReportService reportService) => _reportService = reportService;

    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] ProductStockReportQueryDto query)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
            return BadRequest(new { message = "From date cannot be greater than To date." });

        var result = await _reportService.GetReportAsync(query);
        return Ok(result);
    }
}
