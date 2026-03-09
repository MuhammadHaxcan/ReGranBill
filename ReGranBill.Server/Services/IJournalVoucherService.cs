using ReGranBill.Server.DTOs.JournalVouchers;

namespace ReGranBill.Server.Services;

public interface IJournalVoucherService
{
    Task<List<JournalVoucherDto>> GetAllAsync();
    Task<JournalVoucherDto?> GetByIdAsync(int id);
    Task<string> GetNextNumberAsync();
    Task<JournalVoucherDto> CreateAsync(CreateJournalVoucherRequest request, int userId);
    Task<JournalVoucherDto?> UpdateAsync(int id, CreateJournalVoucherRequest request);
}
