using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/account-closing-report")]
[Authorize(Roles = "Admin")]
public class AccountClosingReportController : ControllerBase
{
    private readonly IAccountClosingReportService _reportService;
    private readonly IPdfService _pdfService;

    public AccountClosingReportController(IAccountClosingReportService reportService, IPdfService pdfService)
    {
        _reportService = reportService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? accountId,
        [FromQuery] int? historyAccountId)
    {
        var result = await _reportService.GetReportAsync(from, to, accountId, historyAccountId);
        return Ok(result);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetReportPdf(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? accountId,
        [FromQuery] int? historyAccountId)
    {
        var result = await _reportService.GetReportAsync(from, to, accountId, historyAccountId);
        var pdfBytes = _pdfService.GenerateAccountClosingReportPdf(result);
        return File(pdfBytes, "application/pdf", "AccountClosingReport.pdf");
    }
}
