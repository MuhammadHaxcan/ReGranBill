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
        settings.UpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);

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

    public async Task<List<VehicleOptionDto>> GetVehiclesAsync()
    {
        return await _db.VehicleOptions
            .AsNoTracking()
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => new VehicleOptionDto
            {
                Id = v.Id,
                Name = v.Name,
                VehicleNumber = v.VehicleNumber,
                SortOrder = v.SortOrder
            })
            .ToListAsync();
    }

    public async Task<List<VehicleOptionDto>> UpdateVehiclesAsync(UpdateVehicleOptionsRequest request)
    {
        var items = request.Vehicles ?? [];
        var existingVehicles = await _db.VehicleOptions.ToListAsync();
        var existingById = existingVehicles.ToDictionary(v => v.Id);
        var requestedIds = items
            .Where(item => item.Id > 0)
            .Select(item => item.Id)
            .ToHashSet();
        var seenNormalized = new HashSet<string>(StringComparer.Ordinal);
        var validatedItems = new List<(int Id, string Name, string VehicleNumber, string NormalizedVehicleNumber)>();
        var resultOrder = new List<VehicleOption>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var name = item.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new RequestValidationException("Vehicle name is required.");

            var number = item.VehicleNumber?.Trim();
            if (string.IsNullOrWhiteSpace(number))
                throw new RequestValidationException("Vehicle number is required.");

            var normalized = NormalizeVehicleNumber(number);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new RequestValidationException("Vehicle number is invalid.");

            if (!seenNormalized.Add(normalized))
                throw new RequestValidationException("Duplicate vehicle numbers are not allowed.");

            validatedItems.Add((item.Id, name, number, normalized));
        }

        var staleVehicles = existingVehicles
            .Where(vehicle => !requestedIds.Contains(vehicle.Id))
            .ToList();
        if (staleVehicles.Count > 0)
        {
            _db.VehicleOptions.RemoveRange(staleVehicles);
        }

        for (var i = 0; i < validatedItems.Count; i++)
        {
            var item = validatedItems[i];
            VehicleOption vehicle;
            if (item.Id > 0 && existingById.TryGetValue(item.Id, out var existing))
            {
                vehicle = existing;
            }
            else
            {
                vehicle = new VehicleOption();
                _db.VehicleOptions.Add(vehicle);
            }

            vehicle.Name = item.Name;
            vehicle.VehicleNumber = item.VehicleNumber;
            vehicle.NormalizedVehicleNumber = item.NormalizedVehicleNumber;
            vehicle.SortOrder = i;
            vehicle.UpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
            resultOrder.Add(vehicle);
        }

        await _db.SaveChangesAsync();

        return resultOrder
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => new VehicleOptionDto
            {
                Id = v.Id,
                Name = v.Name,
                VehicleNumber = v.VehicleNumber,
                SortOrder = v.SortOrder
            })
            .ToList();
    }

    private static CompanySettingsDto MapToDto(CompanySettings? settings) => new()
    {
        CompanyName = settings?.CompanyName ?? string.Empty,
        Address = settings?.Address,
        HasLogo = settings?.LogoBytes is { Length: > 0 },
        UpdatedAt = settings?.UpdatedAt
    };

    private static string NormalizeVehicleNumber(string value)
    {
        return new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }
}
