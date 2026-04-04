using ReGranBill.Server.DTOs.AccountClosingReport;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.MasterReport;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.DTOs.SOA;

namespace ReGranBill.Server.Services;

public interface IPdfService
{
    byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto);
    byte[] GeneratePurchaseVoucherPdf(PurchaseVoucherDto dto);
    byte[] GenerateStatementOfAccountPdf(StatementOfAccountDto dto);
    byte[] GenerateMasterReportPdf(MasterReportDto dto, IReadOnlyCollection<string>? visibleColumns = null);
    byte[] GenerateAccountClosingReportPdf(AccountClosingReportDto dto);
    byte[] GenerateProductStockReportPdf(ProductStockReportDto dto, int? selectedMovementProductId = null);
}
