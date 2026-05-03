namespace ReGranBill.Server.DTOs.Roles;

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsAdmin { get; set; }
    public List<string> Pages { get; set; } = new();
    public int UserCount { get; set; }
}
