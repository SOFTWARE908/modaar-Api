using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Authentication.Services;

public interface IDeviceService
{
    Task<Result<bool>> RegisterPushTokenAsync(Guid userId, RegisterDeviceDto request, CancellationToken ct);

    // Logout on this phone: stop pushing to it. Matches by token because that is what the app's
    // logout already sends.
    Task ClearPushTokenAsync(Guid userId, string fcmToken, CancellationToken ct);
}

public sealed class DeviceService : IDeviceService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(ModaarDbContext db, TimeProvider time, ILogger<DeviceService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<bool>> RegisterPushTokenAsync(Guid userId, RegisterDeviceDto request, CancellationToken ct)
    {
        var deviceId = request.DeviceId.Trim();
        var token = request.FcmToken.Trim();
        var now = _time.GetUtcNow();

        // One phone, one account receiving its pushes: if someone else last logged in on this
        // phone, their notifications must stop arriving on it now.
        var others = await _db.UserDevices
            .Where(d => d.FcmToken == token && !(d.UserId == userId && d.DeviceId == deviceId))
            .ToListAsync(ct);
        foreach (var other in others)
        {
            other.FcmToken = null;
            other.FcmTokenUpdatedAt = now;
            other.UpdatedAt = now;
        }

        var device = await _db.UserDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, ct);
        if (device is null)
        {
            device = new UserDevice { Id = Guid.NewGuid(), UserId = userId, DeviceId = deviceId, CreatedAt = now };
            _db.UserDevices.Add(device);
        }

        device.Platform = request.Platform?.Trim().ToLowerInvariant() ?? device.Platform;
        device.FcmToken = token;
        device.FcmTokenUpdatedAt = now;
        device.LastUsedAt = now;
        device.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Push token registered for user {UserId} on device {DeviceId}.", userId, deviceId);
        return Result<bool>.Ok(true);
    }

    public async Task ClearPushTokenAsync(Guid userId, string fcmToken, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        await _db.UserDevices
            .Where(d => d.UserId == userId && d.FcmToken == fcmToken.Trim())
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.FcmToken, (string?)null)
                .SetProperty(d => d.FcmTokenUpdatedAt, now)
                .SetProperty(d => d.UpdatedAt, now), ct);
    }
}
