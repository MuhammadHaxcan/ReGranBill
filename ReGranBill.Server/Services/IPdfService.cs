using ReGranBill.Server.DTOs.AccountClosingReport;
using ReGranBill.Server.DTOs.CustomerLedger;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.MasterReport;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.PurchaseReturns;
using ReGranBill.Server.DTOs.SOA;
using ReGranBill.Server.DTOs.SaleReturns;

namespace ReGranBill.Server.Services;

public interface IPdfService
{
    byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto);
    byte[] GeneratePurchaseVoucherPdf(PurchaseVoucherDto dto);
    byte[] GenerateSaleReturnPdf(SaleReturnDto dto);
    byte[] GeneratePurchaseReturnPdf(PurchaseReturnDto dto);
    byte[] GenerateStatementOfAccountPdf(StatementOfAccountDto dto);
    byte[] GenerateMasterReportPdf(MasterReportDto dto, IReadOnlyCollection<string>? visibleColumns = null);
    byte[] GenerateAccountClosingReportPdf(AccountClosingReportDto dto);
    byte[] GenerateProductStockReportPdf(ProductStockReportDto dto, int? selectedMovementProductId = null);
    byte[] GenerateCustomerLedgerPdf(CustomerLedgerDto dto);
}
