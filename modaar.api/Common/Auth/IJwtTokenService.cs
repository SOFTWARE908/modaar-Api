using modaar.api.Features.Users.Entities;

namespace modaar.api.Common.Auth;

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashToken(string rawToken);
}
