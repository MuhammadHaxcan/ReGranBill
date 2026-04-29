namespace ReGranBill.Server.DTOs.Auth;

public class LoginAttemptResult
{
    public LoginResponse? Response { get; set; }
    public string? ErrorMessage { get; set; }
}
