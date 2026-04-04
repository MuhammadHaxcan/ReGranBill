using Microsoft.AspNetCore.Http;

namespace ReGranBill.Server.DTOs.CompanySettings;

public class UpdateCompanySettingsRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public IFormFile? Logo { get; set; }
}
