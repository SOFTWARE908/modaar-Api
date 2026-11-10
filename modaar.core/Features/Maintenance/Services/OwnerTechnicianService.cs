using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface IOwnerTechnicianService
{
    Task<Result<List<OwnerTechnicianDto>>> ListAsync(Guid ownerUserId, AccountType role, CancellationToken ct);
    Task<Result<OwnerTechnicianDto>> AddAsync(Guid ownerUserId, Guid adminUserId, AccountType role, AddOwnerTechnicianDto request, CancellationToken ct);
    Task<Result<bool>> RemoveAsync(Guid ownerUserId, Guid technicianId, AccountType role, CancellationToken ct);
}

// The admin keeps each owner's private technicians. Unlinking keeps the technician (they may
// serve other owners, and past requests still name them).
public sealed class OwnerTechnicianService : IOwnerTechnicianService
{
    private const string AdminOnly = "Switch to your admin account to manage owners' technicians.";

    private readonly ModaarDbContext _db;
    private readonly IRequestLanguage _language;
    private readonly TimeProvider _time;
    private readonly ILogger<OwnerTechnicianService> _logger;

    public OwnerTechnicianService(ModaarDbContext db, IRequestLanguage language, TimeProvider time, ILogger<OwnerTechnicianService> logger)
    {
        _db = db;
        _language = language;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<List<OwnerTechnicianDto>>> ListAsync(Guid ownerUserId, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<List<OwnerTechnicianDto>>.Fail(AppErrorCode.Forbidden, AdminOnly);

        if (!await IsOwnerAsync(ownerUserId, ct))
            return Result<List<OwnerTechnicianDto>>.Fail(AppErrorCode.NotFound, "Owner not found.");

        return Result<List<OwnerTechnicianDto>>.Ok(await LoadAsync(ownerUserId, technicianId: null, ct));
    }

    public async Task<Result<OwnerTechnicianDto>> AddAsync(
        Guid ownerUserId, Guid adminUserId, AccountType role, AddOwnerTechnicianDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<OwnerTechnicianDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        if (!await IsOwnerAsync(ownerUserId, ct))
            return Result<OwnerTechnicianDto>.Fail(AppErrorCode.NotFound, "Owner not found.");

        var serviceTypeIds = request.ServiceTypeIds?.Distinct().ToList();
        if (serviceTypeIds is { Count: > 0 } &&
            await _db.MaintenanceServiceTypes.CountAsync(s => serviceTypeIds.Contains(s.Id), ct) != serviceTypeIds.Count)
            return Result<OwnerTechnicianDto>.Fail(AppErrorCode.ValidationFailed, "One or more service types are unknown.");

        // Same keying as sign-in: country code with "+", national number without the trunk zero.
        var countryCode = request.CountryCode.Trim().StartsWith('+') ? request.CountryCode.Trim() : "+" + request.CountryCode.Trim();
        var phone = request.PhoneNumber.Trim().TrimStart('0');
        var now = _time.GetUtcNow();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.CountryCode == countryCode && u.PhoneNumber == phone && !u.IsDeleted, ct);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return Result<OwnerTechnicianDto>.Fail(AppErrorCode.ValidationFailed, "fullName is required for a number with no account.");

            user = new User
            {
                Id = Guid.NewGuid(),
                CountryCode = countryCode,
                PhoneNumber = phone,
                FullName = request.FullName.Trim(),
                AccountTypes = [AccountType.Technician],
                ProfileStatus = ProfileStatus.Complete,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Users.Add(user);
        }
        else if (!user.AccountTypes.Contains(AccountType.Technician))
        {
            user.AccountTypes = [.. user.AccountTypes, AccountType.Technician];
            user.UpdatedAt = now;
        }

        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.UserId == user.Id, ct);
        if (technician is null)
        {
            if (string.IsNullOrWhiteSpace(request.RoleAr) || string.IsNullOrWhiteSpace(request.RoleEn))
                return Result<OwnerTechnicianDto>.Fail(AppErrorCode.ValidationFailed, "roleAr and roleEn are required for a new technician.");

            technician = new Technician
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleAr = request.RoleAr.Trim(),
                RoleEn = request.RoleEn.Trim(),
                IsAvailable = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Technicians.Add(technician);
        }
        else if (await _db.OwnerTechnicians.AnyAsync(o => o.OwnerUserId == ownerUserId && o.TechnicianId == technician.Id, ct))
        {
            return Result<OwnerTechnicianDto>.Fail(AppErrorCode.Conflict, "That technician is already linked to this owner.");
        }

        if (serviceTypeIds is not null)
        {
            var current = await _db.TechnicianServiceTypes.Where(s => s.TechnicianId == technician.Id).ToListAsync(ct);
            _db.TechnicianServiceTypes.RemoveRange(current);
            _db.TechnicianServiceTypes.AddRange(serviceTypeIds.Select(id => new TechnicianServiceType { TechnicianId = technician.Id, ServiceTypeId = id }));
        }

        _db.OwnerTechnicians.Add(new OwnerTechnician
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            TechnicianId = technician.Id,
            CreatedByUserId = adminUserId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Technician {TechnicianId} linked to owner {OwnerId} by admin {AdminId}.", technician.Id, ownerUserId, adminUserId);

        return Result<OwnerTechnicianDto>.Ok((await LoadAsync(ownerUserId, technician.Id, ct)).Single());
    }

    public async Task<Result<bool>> RemoveAsync(Guid ownerUserId, Guid technicianId, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<bool>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var link = await _db.OwnerTechnicians.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId && o.TechnicianId == technicianId, ct);
        if (link is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "That technician isn't linked to this owner.");

        _db.OwnerTechnicians.Remove(link);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private Task<bool> IsOwnerAsync(Guid ownerUserId, CancellationToken ct) =>
        _db.Users.AnyAsync(u => u.Id == ownerUserId && !u.IsDeleted && u.AccountTypes.Contains(AccountType.Owner), ct);

    private async Task<List<OwnerTechnicianDto>> LoadAsync(Guid ownerUserId, Guid? technicianId, CancellationToken ct)
    {
        var rows = await _db.OwnerTechnicians
            .AsNoTracking()
            .Where(o => o.OwnerUserId == ownerUserId && (technicianId == null || o.TechnicianId == technicianId))
            .Join(_db.Technicians, o => o.TechnicianId, t => t.Id, (o, t) => new { o.CreatedAt, t })
            .Select(x => new
            {
                x.t.Id,
                x.t.RoleAr,
                x.t.RoleEn,
                x.t.IsAvailable,
                HasBroker = x.t.BrokerId != null,
                x.CreatedAt,
                User = _db.Users.Where(u => u.Id == x.t.UserId)
                    .Select(u => new { u.FullName, Phone = u.CountryCode + u.PhoneNumber, u.ProfileImageUrl }).FirstOrDefault(),
                Specialties = _db.TechnicianServiceTypes.Where(s => s.TechnicianId == x.t.Id)
                    .Join(_db.MaintenanceServiceTypes, s => s.ServiceTypeId, st => st.Id, (s, st) => new { st.NameAr, st.NameEn, st.SortOrder })
                    .ToList(),
                Owners = _db.OwnerTechnicians.Count(o => o.TechnicianId == x.t.Id)
            })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => new OwnerTechnicianDto
        {
            TechnicianId = r.Id,
            Name = r.User?.FullName ?? string.Empty,
            PhoneNumber = r.User?.Phone ?? string.Empty,
            AvatarUrl = r.User?.ProfileImageUrl,
            Role = _language.Pick(r.RoleAr, r.RoleEn),
            Specialties = r.Specialties.OrderBy(s => s.SortOrder).Select(s => _language.Pick(s.NameAr, s.NameEn)).ToList(),
            IsAvailable = r.IsAvailable,
            HasBroker = r.HasBroker,
            OwnersCount = r.Owners,
            LinkedAt = r.CreatedAt
        }).ToList();
    }
}
