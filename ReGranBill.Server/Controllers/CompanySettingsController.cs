using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.CompanySettings;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/company-settings")]
[Authorize]
public class CompanySettingsController : ControllerBase
{
    private readonly ICompanySettingsService _companySettingsService;

    public CompanySettingsController(ICompanySettingsService companySettingsService) =>
        _companySettingsService = companySettingsService;

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get() => Ok(await _companySettingsService.GetAsync());

    [HttpPut]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromForm] UpdateCompanySettingsRequest request) =>
        Ok(await _companySettingsService.UpdateAsync(request));

    [HttpGet("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetLogo()
    {
        var logo = await _companySettingsService.GetLogoAsync();
        if (logo == null)
            return NotFound();

        return File(logo.Value.Content, logo.Value.ContentType);
    }

    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles() =>
        Ok(await _companySettingsService.GetVehiclesAsync());

    [HttpPut("vehicles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVehicles([FromBody] UpdateVehicleOptionsRequest request) =>
        Ok(await _companySettingsService.UpdateVehiclesAsync(request));
}
