using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.CompanySettings;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/company-settings")]
[Authorize(Roles = "Admin")]
public class CompanySettingsController : ControllerBase
{
    private readonly ICompanySettingsService _companySettingsService;

    public CompanySettingsController(ICompanySettingsService companySettingsService) =>
        _companySettingsService = companySettingsService;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _companySettingsService.GetAsync());

    [HttpPut]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Update([FromForm] UpdateCompanySettingsRequest request) =>
        Ok(await _companySettingsService.UpdateAsync(request));

    [HttpGet("logo")]
    public async Task<IActionResult> GetLogo()
    {
        var logo = await _companySettingsService.GetLogoAsync();
        if (logo == null)
            return NotFound();

        return File(logo.Value.Content, logo.Value.ContentType);
    }
}
