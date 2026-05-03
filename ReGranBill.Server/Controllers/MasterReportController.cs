using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/master-report")]
[Authorize]
[RequirePage("master-report")]
public class MasterReportController : ControllerBase
{
    private readonly IMasterReportService _reportService;
    private readonly IPdfService _pdfService;

    public MasterReportController(IMasterReportService reportService, IPdfService pdfService)
    {
        _reportService = reportService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? categoryId,
        [FromQuery] int? accountId)
    {
        var result = await _reportService.GetReportAsync(from, to, categoryId, accountId);
        return Ok(result);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetReportPdf(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? categoryId,
        [FromQuery] int? accountId,
        [FromQuery] string? columns)
    {
        var result = await _reportService.GetReportAsync(from, to, categoryId, accountId);
        var visibleColumns = ParseVisibleColumns(columns);
        var pdfBytes = _pdfService.GenerateMasterReportPdf(result, visibleColumns);
        return File(pdfBytes, "application/pdf", "MasterReport.pdf");
    }

    private static IReadOnlyCollection<string> ParseVisibleColumns(string? columns)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "voucher", "date", "description", "account", "quantity", "rate", "debit", "credit", "balance"
        };

        if (string.IsNullOrWhiteSpace(columns))
            return allowed.ToArray();

        var parsed = columns
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(column => allowed.Contains(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parsed.Length > 0 ? parsed : allowed.ToArray();
    }
}
