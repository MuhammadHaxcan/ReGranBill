namespace ReGranBill.Server.Entities;

public class DcCartage
{
    public int Id { get; set; }
    public int DcId { get; set; }
    public int TransporterId { get; set; }
    public decimal Amount { get; set; }

    public DeliveryChallan DeliveryChallan { get; set; } = null!;
    public Account Transporter { get; set; } = null!;
}
