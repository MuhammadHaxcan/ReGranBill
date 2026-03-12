using ReGranBill.Server.DTOs.CashVouchers;

namespace ReGranBill.Server.Services;

public interface ICashVoucherService
{
    Task<string> GetNextReceiptNumberAsync();
    Task<string> GetNextPaymentNumberAsync();
    Task<CashVoucherDto?> GetReceiptByIdAsync(int id);
    Task<CashVoucherDto?> GetPaymentByIdAsync(int id);
    Task<CashVoucherDto> CreateReceiptAsync(CreateCashVoucherRequest request, int userId);
    Task<CashVoucherDto> CreatePaymentAsync(CreateCashVoucherRequest request, int userId);
    Task<CashVoucherDto?> UpdateReceiptAsync(int id, CreateCashVoucherRequest request);
    Task<CashVoucherDto?> UpdatePaymentAsync(int id, CreateCashVoucherRequest request);
}
