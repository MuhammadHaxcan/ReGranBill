namespace ReGranBill.Server.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = false;
    public bool IsAdmin { get; set; } = false;
    public DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly UpdatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public ICollection<RolePage> Pages { get; set; } = new List<RolePage>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
