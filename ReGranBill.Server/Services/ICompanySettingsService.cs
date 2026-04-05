using ReGranBill.Server.DTOs.CompanySettings;

namespace ReGranBill.Server.Services;

public interface ICompanySettingsService
{
    Task<CompanySettingsDto> GetAsync();
    Task<CompanySettingsDto> UpdateAsync(UpdateCompanySettingsRequest request);
    Task<(byte[] Content, string ContentType)?> GetLogoAsync();
    Task<List<VehicleOptionDto>> GetVehiclesAsync();
    Task<List<VehicleOptionDto>> UpdateVehiclesAsync(UpdateVehicleOptionsRequest request);
}
