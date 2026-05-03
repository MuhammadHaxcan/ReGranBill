namespace ReGranBill.Server.Entities;

public class RolePage
{
    public int RoleId { get; set; }
    public string PageKey { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
}
