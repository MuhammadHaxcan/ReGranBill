namespace ReGranBill.Server.DTOs.Users;

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
