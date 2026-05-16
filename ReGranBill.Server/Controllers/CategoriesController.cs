using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.Categories;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    // GetAll is reference data — every voucher page lists categories. No page gate.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _categoryService.GetAllAsync());
    }

    // Filtered list — used by pages that should only show categories that actually contain
    // accounts of the relevant type/role (e.g., Washing input → UnwashedMaterial,
    // Customer Ledger → Party with Customer/Vendor/Both).
    [HttpGet("filtered")]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] string? accountTypes,
        [FromQuery] string? partyRoles)
    {
        var types = ParseEnumList<AccountType>(accountTypes);
        var roles = ParseEnumList<PartyRole>(partyRoles);
        return Ok(await _categoryService.GetFilteredAsync(types, roles));
    }

    private static IReadOnlyList<T> ParseEnumList<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => Enum.TryParse<T>(token, true, out var parsed) ? parsed : (T?)null)
            .Where(parsed => parsed.HasValue)
            .Select(parsed => parsed!.Value)
            .Distinct()
            .ToList();
    }

    [HttpPost]
    [RequirePage("metadata")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var result = await _categoryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [RequirePage("metadata")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest request)
    {
        var result = await _categoryService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePage("metadata")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _categoryService.DeleteAsync(id);
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
