using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Common.Auth;

public interface IJwtTokenService
{
    //string CreateAccessToken(User user);
    string CreateAccessToken(User user, AccountType? activeRole);
    string CreateRefreshToken();
    string HashToken(string rawToken);
}
