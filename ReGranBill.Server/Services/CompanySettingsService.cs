using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.CompanySettings;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Exceptions;

namespace ReGranBill.Server.Services;

public class CompanySettingsService : ICompanySettingsService
{
    private static readonly HashSet<string> AllowedLogoContentTypes =
    [
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/svg+xml",
        "image/webp"
    ];

    private readonly AppDbContext _db;

    public CompanySettingsService(AppDbContext db) => _db = db;

    public async Task<CompanySettingsDto> GetAsync()
    {
        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        return MapToDto(settings);
    }

    public async Task<CompanySettingsDto> UpdateAsync(UpdateCompanySettingsRequest request)
    {
        var companyName = request.CompanyName.Trim();
        if (string.IsNullOrWhiteSpace(companyName))
            throw new RequestValidationException("Company name is required.");

        var settings = await _db.CompanySettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new CompanySettings();
            _db.CompanySettings.Add(settings);
        }

        settings.CompanyName = companyName;
        settings.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        settings.UpdatedAt = DateTime.UtcNow;

        if (request.Logo != null)
        {
            if (request.Logo.Length == 0)
                throw new RequestValidationException("Uploaded logo file is empty.");

            var contentType = request.Logo.ContentType?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType) || !AllowedLogoContentTypes.Contains(contentType))
                throw new RequestValidationException("Logo must be a valid image file.");

            await using var stream = request.Logo.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            settings.LogoBytes = memoryStream.ToArray();
            settings.LogoContentType = contentType;
        }

        await _db.SaveChangesAsync();
        return MapToDto(settings);
    }

    public async Task<(byte[] Content, string ContentType)?> GetLogoAsync()
    {
        var settings = await _db.CompanySettings
            .AsNoTracking()
            .Where(s => s.LogoBytes != null && s.LogoContentType != null)
            .Select(s => new { s.LogoBytes, s.LogoContentType })
            .FirstOrDefaultAsync();

        if (settings?.LogoBytes == null || string.IsNullOrWhiteSpace(settings.LogoContentType))
            return null;

        return (settings.LogoBytes, settings.LogoContentType);
    }

    private static CompanySettingsDto MapToDto(CompanySettings? settings) => new()
    {
        CompanyName = settings?.CompanyName ?? string.Empty,
        Address = settings?.Address,
        HasLogo = settings?.LogoBytes is { Length: > 0 },
        UpdatedAt = settings?.UpdatedAt
    };
}
