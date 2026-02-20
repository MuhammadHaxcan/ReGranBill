using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/delivery-challans")]
[Authorize]
public class DeliveryChallansController : ControllerBase
{
    private readonly IDeliveryChallanService _dcService;

    public DeliveryChallansController(IDeliveryChallanService dcService) => _dcService = dcService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _dcService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dc = await _dcService.GetByIdAsync(id);
        if (dc == null) return NotFound();
        return Ok(dc);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber()
    {
        var dcNumber = await _dcService.GetNextNumberAsync();
        return Ok(new { dcNumber });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDcRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _dcService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDcRequest request)
    {
        var result = await _dcService.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("{id}/rates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRates(int id, [FromBody] UpdateDcRatesRequest request)
    {
        if (!await _dcService.UpdateRatesAsync(id, request)) return NotFound();
        return Ok(await _dcService.GetByIdAsync(id));
    }
}
