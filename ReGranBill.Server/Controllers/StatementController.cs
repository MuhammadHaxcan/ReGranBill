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

    public StatementController(IStatementService statementService) => _statementService = statementService;

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetStatement(int accountId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _statementService.GetStatementAsync(accountId, fromDate, toDate);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
