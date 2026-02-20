using ReGranBill.Server.Entities;

namespace ReGranBill.Server.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
