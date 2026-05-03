namespace ReGranBill.Server.DTOs.Users;

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
}
