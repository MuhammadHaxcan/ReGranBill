namespace ReGranBill.Server.DTOs.Roles;

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Pages { get; set; } = new();
}
