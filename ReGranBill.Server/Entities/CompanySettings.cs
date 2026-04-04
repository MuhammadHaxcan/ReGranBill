namespace ReGranBill.Server.Entities;

public class CompanySettings
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public byte[]? LogoBytes { get; set; }
    public string? LogoContentType { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
