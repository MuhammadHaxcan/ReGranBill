using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.SOA;

namespace ReGranBill.Server.Services;

public interface IPdfService
{
    byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto);
    byte[] GeneratePurchaseVoucherPdf(PurchaseVoucherDto dto);
    byte[] GenerateStatementOfAccountPdf(StatementOfAccountDto dto);
}
