using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.Categories;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    [HttpGet]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _categoryService.GetAllAsync());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var result = await _categoryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest request)
    {
        var result = await _categoryService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _categoryService.DeleteAsync(id);
        if (error != null) return Conflict(new { statusCode = StatusCodes.Status409Conflict, message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}
