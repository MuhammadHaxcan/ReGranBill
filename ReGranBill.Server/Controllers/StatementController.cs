using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/statements")]
[Authorize(Roles = "Admin")]
public class StatementController : ControllerBase
{
    private readonly IStatementService _statementService;
    private readonly IPdfService _pdfService;

    public StatementController(IStatementService statementService, IPdfService pdfService)
    {
        _statementService = statementService;
        _pdfService = pdfService;
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetStatement(int accountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var result = await _statementService.GetStatementAsync(accountId, fromDate, toDate);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{accountId}/pdf")]
    public async Task<IActionResult> GetStatementPdf(int accountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var result = await _statementService.GetStatementAsync(accountId, fromDate, toDate);
        if (result == null) return NotFound();

        var pdfBytes = _pdfService.GenerateStatementOfAccountPdf(result);
        return File(pdfBytes, "application/pdf", $"Statement-{result.AccountName}.pdf");
    }
}
