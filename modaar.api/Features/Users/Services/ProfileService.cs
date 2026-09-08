using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public sealed class ProfileService : IProfileService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public ProfileService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<AuthResult<UserProfileDto>> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return AuthResult<UserProfileDto>.Fail(AuthErrorCode.UserNotFound, "User not found.");

        return AuthResult<UserProfileDto>.Ok(ToDto(user));
    }

    public async Task<AuthResult<UserProfileDto>> UpdateAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return AuthResult<UserProfileDto>.Fail(AuthErrorCode.UserNotFound, "User not found.");

        if (request.AccountType is { } accountType)
        {
            if (accountType == AccountType.Technician)
                return AuthResult<UserProfileDto>.Fail(AuthErrorCode.AccountTypeNotAllowed,
                    "Technician accounts cannot be selected from the mobile app.");
            user.AccountType = accountType;
        }

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (email != user.Email &&
                await _db.Users.AnyAsync(u => !u.IsDeleted && u.Id != userId && u.Email == email, ct))
            {
                return AuthResult<UserProfileDto>.Fail(AuthErrorCode.ProfileFieldTaken,
                    "That email is already used by another account.");
            }

            // Changing the address invalidates any previous confirmation of it.
            if (email != user.Email) user.IsEmailVerified = false;
            user.Email = email;
        }

        if (request.NationalId is not null)
        {
            var nationalId = request.NationalId.Trim();
            if (nationalId != user.NationalId &&
                await _db.Users.AnyAsync(u => !u.IsDeleted && u.Id != userId && u.NationalId == nationalId, ct))
            {
                return AuthResult<UserProfileDto>.Fail(AuthErrorCode.ProfileFieldTaken,
                    "That national ID is already used by another account.");
            }

            user.NationalId = nationalId;
        }

        if (request.FullName is not null)
            user.FullName = request.FullName.Trim();

        if (request.ProfileImageUrl is not null)
            user.ProfileImageUrl = request.ProfileImageUrl.Trim();

        // AccountType always holds a value (it defaults to Tenant at sign-up), so the name is what
        // is actually outstanding on a fresh account.
        if (!string.IsNullOrWhiteSpace(user.FullName))
            user.ProfileStatus = ProfileStatus.Complete;

        user.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return AuthResult<UserProfileDto>.Ok(ToDto(user));
    }

    private static UserProfileDto ToDto(User user) => new()
    {
        PhoneNumber = user.PhoneNumber,
        CountryCode = user.CountryCode,
        AccountType = user.AccountType,
        ProfileStatus = user.ProfileStatus,
        FullName = user.FullName,
        Email = user.Email,
        NationalId = user.NationalId,
        ProfileImageUrl = user.ProfileImageUrl
    };
}
