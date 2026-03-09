using ReGranBill.Server.DTOs.VoucherEditor;

namespace ReGranBill.Server.Services;

public interface IVoucherEditorService
{
    Task<VoucherLedgerDto?> FindByTypeAndNumberAsync(string voucherType, string voucherNumber);
    Task<VoucherLedgerDto?> UpdateAsync(UpdateVoucherLedgerRequest request);
}
