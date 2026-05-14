using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.Authorization;
using ReGranBill.Server.DTOs.Formulations;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/formulations")]
[Authorize]
public class FormulationsController : ControllerBase
{
    private readonly IFormulationService _service;

    public FormulationsController(IFormulationService service)
    {
        _service = service;
    }

    // Read endpoints: a production-voucher operator needs to see and apply formulations
    // even without formulation-management permission, so accept either page key.
    [HttpGet]
    [RequireAnyPage("formulations", "production-voucher")]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    [RequireAnyPage("formulations", "production-voucher")]
    public async Task<IActionResult> GetById(int id)
    {
        var formulation = await _service.GetByIdAsync(id);
        if (formulation == null) return NotFound();
        return Ok(formulation);
    }

    [HttpPost("{id}/apply")]
    [RequireAnyPage("formulations", "production-voucher")]
    public async Task<IActionResult> Apply(int id, [FromBody] ApplyFormulationRequest request)
    {
        var result = await _service.ApplyAsync(id, request.TotalInputKg);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [RequirePage("formulations")]
    public async Task<IActionResult> Create([FromBody] CreateFormulationRequest request)
    {
        if (!this.TryGetAuthenticatedUserId(out var userId))
            return this.InvalidUserSession();

        var result = await _service.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [RequirePage("formulations")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateFormulationRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePage("formulations")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _service.DeleteAsync(id)) return NotFound();
        return NoContent();
    }
}
