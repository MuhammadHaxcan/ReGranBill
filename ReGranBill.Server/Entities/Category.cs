namespace ReGranBill.Server.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
