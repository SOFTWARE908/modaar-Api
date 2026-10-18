using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public sealed class ProfileService : IProfileService
{
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    // Extension allow-list, same reasoning as maintenance uploads: the content type the client
    // sends proves nothing.
    private static readonly HashSet<string> AvatarExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".heic" };

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IHttpContextAccessor _http;
    private readonly string _uploadsRoot;

    public ProfileService(ModaarDbContext db, TimeProvider time, IConfiguration config, IHttpContextAccessor http)
    {
        _db = db;
        _time = time;
        _http = http;
        _uploadsRoot = config["Uploads:Root"]
            ?? throw new InvalidOperationException("Uploads:Root is not configured.");
    }

    public async Task<Result<UserProfileDto>> UploadAvatarAsync(Guid userId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return Result<UserProfileDto>.Fail(AppErrorCode.ValidationFailed, "The file is empty.");

        if (file.Length > MaxAvatarBytes)
            return Result<UserProfileDto>.Fail(AppErrorCode.ValidationFailed, "The image exceeds 5 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AvatarExtensions.Contains(extension))
            return Result<UserProfileDto>.Fail(AppErrorCode.ValidationFailed, "Upload a JPG, PNG, WEBP or HEIC image.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return Result<UserProfileDto>.Fail(AppErrorCode.NotFound, "User not found.");

        // A new name each time rather than overwriting: the old URL may be cached on the phone,
        // and a changed URL is what makes the app fetch the new picture.
        var storedName = $"{userId:N}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var folder = Path.Combine(_uploadsRoot, "avatars");
        Directory.CreateDirectory(folder);

        await using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew))
            await file.CopyToAsync(stream, ct);

        var previousUrl = user.ProfileImageUrl;

        var request = _http.HttpContext!.Request;
        user.ProfileImageUrl = $"{request.Scheme}://{request.Host}/uploads/avatars/{storedName}";
        user.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        DeletePreviousAvatar(previousUrl, userId, folder);

        return Result<UserProfileDto>.Ok(ToDto(user));
    }

    // Only a file this endpoint wrote for this user: ProfileImageUrl can also be any URL sent
    // through PUT /profile, and that must never translate into deleting an arbitrary path.
    private static void DeletePreviousAvatar(string? previousUrl, Guid userId, string folder)
    {
        if (string.IsNullOrEmpty(previousUrl) || !previousUrl.Contains("/uploads/avatars/"))
            return;

        // The last path segment, without any query string. Path.GetFileName also drops any
        // "..\" a crafted URL might carry.
        var name = Path.GetFileName(previousUrl.Split('?', '#')[0]);

        if (!name.StartsWith($"{userId:N}-", StringComparison.Ordinal))
            return;

        try { File.Delete(Path.Combine(folder, name)); }
        catch (IOException) { /* Best effort: an orphaned old picture is harmless. */ }
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

        if (request.AccountTypes is not null)
            user.AccountTypes = request.AccountTypes.ToList();

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

        if (request.Language is not null)
            user.PreferredLanguage = request.Language;

        user.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return AuthResult<UserProfileDto>.Ok(ToDto(user));
    }

    private static UserProfileDto ToDto(User user) => new()
    {
        PhoneNumber = user.PhoneNumber,
        CountryCode = user.CountryCode,
        AccountType = user.PrimaryAccountType,
        AccountTypes = user.AccountTypes,
        ProfileStatus = user.ProfileStatus,
        FullName = user.FullName,
        Email = user.Email,
        NationalId = user.NationalId,
        ProfileImageUrl = user.ProfileImageUrl,
        DateOfBirth = user.DateOfBirth,
        Nationality = user.Nationality,
        Id = user.Id,
        Language = user.PreferredLanguage
    };
}
