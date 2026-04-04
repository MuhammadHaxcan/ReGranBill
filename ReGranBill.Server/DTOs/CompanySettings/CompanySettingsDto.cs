namespace ReGranBill.Server.DTOs.CompanySettings;

public class CompanySettingsDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool HasLogo { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
