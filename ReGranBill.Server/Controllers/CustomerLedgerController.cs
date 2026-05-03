using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/customer-ledger")]
[Authorize]
[RequirePage("customer-ledger")]
public class CustomerLedgerController : ControllerBase
{
    private readonly ICustomerLedgerService _ledgerService;
    private readonly IPdfService _pdfService;

    public CustomerLedgerController(ICustomerLedgerService ledgerService, IPdfService pdfService)
    {
        _ledgerService = ledgerService;
        _pdfService = pdfService;
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetLedger(int accountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var result = await _ledgerService.GetLedgerAsync(accountId, fromDate, toDate);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllLedgers([FromQuery] string partyType, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var results = await _ledgerService.GetAllLedgersAsync(partyType, fromDate, toDate);
        return Ok(results);
    }

    [HttpGet("{accountId}/pdf")]
    public async Task<IActionResult> GetLedgerPdf(int accountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
    {
        var result = await _ledgerService.GetLedgerAsync(accountId, fromDate, toDate);
        if (result == null) return NotFound();

        var pdfBytes = _pdfService.GenerateCustomerLedgerPdf(result);
        return File(pdfBytes, "application/pdf", $"Ledger-{result.AccountName}.pdf");
    }
}
