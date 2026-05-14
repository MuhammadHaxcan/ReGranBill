using ReGranBill.Server.DTOs.Formulations;

namespace ReGranBill.Server.Services;

public interface IFormulationService
{
    Task<List<FormulationDto>> GetAllAsync();
    Task<FormulationDto?> GetByIdAsync(int id);
    Task<FormulationDto> CreateAsync(CreateFormulationRequest request, int userId);
    Task<FormulationDto?> UpdateAsync(int id, CreateFormulationRequest request);
    Task<bool> DeleteAsync(int id);
    Task<ApplyFormulationResponse?> ApplyAsync(int id, decimal totalInputKg);
}
