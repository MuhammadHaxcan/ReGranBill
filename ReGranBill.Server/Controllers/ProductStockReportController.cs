using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/product-stock-report")]
[Authorize]
[RequirePage("product-stock-report")]
public class ProductStockReportController : ControllerBase
{
    private readonly IProductStockReportService _reportService;
    private readonly IPdfService _pdfService;

    public ProductStockReportController(IProductStockReportService reportService, IPdfService pdfService)
    {
        _reportService = reportService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] ProductStockReportQueryDto query)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
            return BadRequest(new { statusCode = StatusCodes.Status400BadRequest, message = "From date cannot be greater than To date." });

        var result = await _reportService.GetReportAsync(query);
        return Ok(result);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetReportPdf([FromQuery] ProductStockReportQueryDto query, [FromQuery] int? selectedMovementProductId)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
            return BadRequest(new { statusCode = StatusCodes.Status400BadRequest, message = "From date cannot be greater than To date." });

        var result = await _reportService.GetReportAsync(query);
        var pdfBytes = _pdfService.GenerateProductStockReportPdf(result, selectedMovementProductId);
        return File(pdfBytes, "application/pdf", "ProductStockReport.pdf");
    }
}
