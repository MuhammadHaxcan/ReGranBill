using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.Accounts;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService) => _accountService = accountService;

    // GetAll lists every account — only the Categories & Accounts page needs this.
    [HttpGet]
    [RequirePage("metadata")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _accountService.GetAllAsync());
    }

    // Filtered reads are reference data used by every voucher / report page. No page gate.
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        return Ok(await _accountService.GetCustomersAsync());
    }

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors()
    {
        return Ok(await _accountService.GetVendorsAsync());
    }

    [HttpGet("transporters")]
    public async Task<IActionResult> GetTransporters()
    {
        return Ok(await _accountService.GetTransportersAsync());
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        return Ok(await _accountService.GetProductsAsync());
    }

    [HttpGet("journal")]
    public async Task<IActionResult> GetJournalAccounts()
    {
        return Ok(await _accountService.GetJournalAccountsAsync());
    }

    [HttpGet("by-category/{categoryId:int}")]
    public async Task<IActionResult> GetByCategory(int categoryId)
    {
        return Ok(await _accountService.GetByCategoryAsync(categoryId));
    }

    [HttpPost]
    [RequirePage("metadata")]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
    {
        var result = await _accountService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [RequirePage("metadata")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateAccountRequest request)
    {
        var result = await _accountService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePage("metadata")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _accountService.DeleteAsync(id);
        if (result.Blocked != null)
            return Conflict(new
            {
                statusCode = StatusCodes.Status409Conflict,
                message = result.Blocked.Message,
                vouchers = result.Blocked.Vouchers,
                totalCount = result.Blocked.TotalCount
            });
        if (result.Error != null)
            return Conflict(new { statusCode = StatusCodes.Status409Conflict, message = result.Error });
        if (!result.Success) return NotFound();
        return NoContent();
    }
}
