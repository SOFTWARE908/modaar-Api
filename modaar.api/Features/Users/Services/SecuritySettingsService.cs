
namespace modaar.api.Features.Users.Services
{
    using Microsoft.EntityFrameworkCore;
    using modaar.api.Common.Results;
    using modaar.api.Features.Users.Dtos;
    using modaar.api.Persistence;

    public sealed class SecuritySettingsService : ISecuritySettingsService
    {
        private readonly ModaarDbContext _db;
        private readonly TimeProvider _time;

        public SecuritySettingsService(ModaarDbContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        public async Task<Result<SecuritySettingsDto>> GetAsync(Guid userId, CancellationToken ct)
        {
            var settings = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => new SecuritySettingsDto
                {
                    RememberMe = u.RememberMe,
                    FaceIdEnabled = u.FaceIdEnabled,
                    BiometricEnabled = u.BiometricEnabled
                })
                .FirstOrDefaultAsync(ct);

            return settings is null
                ? Result<SecuritySettingsDto>.Fail(AppErrorCode.NotFound, "User not found.")
                : Result<SecuritySettingsDto>.Ok(settings);
        }

        public async Task<Result<SecuritySettingsDto>> UpdateAsync(
            Guid userId, UpdateSecuritySettingsDto request, CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
            if (user is null)
                return Result<SecuritySettingsDto>.Fail(AppErrorCode.NotFound, "User not found.");

            if (request.RememberMe is { } rememberMe) user.RememberMe = rememberMe;
            if (request.FaceIdEnabled is { } faceId) user.FaceIdEnabled = faceId;
            if (request.BiometricEnabled is { } biometric) user.BiometricEnabled = biometric;

            user.UpdatedAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct);

            return Result<SecuritySettingsDto>.Ok(new SecuritySettingsDto
            {
                RememberMe = user.RememberMe,
                FaceIdEnabled = user.FaceIdEnabled,
                BiometricEnabled = user.BiometricEnabled
            });
        }
    }
}