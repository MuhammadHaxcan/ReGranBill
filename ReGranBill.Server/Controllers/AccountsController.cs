using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _accountService.GetAllAsync());
    }

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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
    {
        var result = await _accountService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateAccountRequest request)
    {
        var result = await _accountService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _accountService.DeleteAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}
