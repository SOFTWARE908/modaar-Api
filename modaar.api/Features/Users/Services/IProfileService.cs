using modaar.api.Common.Auth;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Users.Services;

public interface IProfileService
{
    Task<AuthResult<UserProfileDto>> GetAsync(Guid userId, CancellationToken ct);

    // Partial update: only the fields present on the request are touched.
    Task<AuthResult<UserProfileDto>> UpdateAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct);
}
