using ReGranBill.Server.DTOs.DeliveryChallans;

namespace ReGranBill.Server.Services;

public interface IPdfService
{
    byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto);
}
