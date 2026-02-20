using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class PartyDetail
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public PartyRole PartyRole { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }

    public Account Account { get; set; } = null!;
}
