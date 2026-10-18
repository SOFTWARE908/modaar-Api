using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services
{
    public interface IMaintenanceServiceTypeService
    {
        Task<Result<IReadOnlyList<MaintenanceServiceTypeDto>>> ListAsync(CancellationToken ct);

        Task<Result<IReadOnlyList<MaintenanceServiceTypeAdminDto>>> ListForAdminAsync(
            AccountType activeRole, CancellationToken ct);

        Task<Result<MaintenanceServiceTypeAdminDto>> CreateAsync(
            CreateMaintenanceServiceTypeDto dto, AccountType activeRole, CancellationToken ct);

        Task<Result<MaintenanceServiceTypeAdminDto>> UpdateAsync(
            Guid id, UpdateMaintenanceServiceTypeDto dto, AccountType activeRole, CancellationToken ct);

        Task<Result<bool>> DeactivateAsync(Guid id, AccountType activeRole, CancellationToken ct);
    }

    public sealed class MaintenanceServiceTypeService : IMaintenanceServiceTypeService
    {
        private readonly ModaarDbContext _db;
        private readonly TimeProvider _time;
        private readonly IRequestLanguage _language;
        private readonly ILogger<MaintenanceServiceTypeService> _logger;

        public MaintenanceServiceTypeService(
            ModaarDbContext db,
            TimeProvider time,
            IRequestLanguage language,
            ILogger<MaintenanceServiceTypeService> logger)
        {
            _db = db;
            _time = time;
            _language = language;
            _logger = logger;
        }

        // The picker. Active only, in order — an inactive category is not offered, though an existing
        // request filed under one still resolves through the projection helper below.
        public async Task<Result<IReadOnlyList<MaintenanceServiceTypeDto>>> ListAsync(CancellationToken ct)
        {
            // Both names come back and Pick runs after ToListAsync: it is a C# method and EF cannot
            // translate it into SQL.
            var rows = await _db.MaintenanceServiceTypes
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .Select(t => new { t.Id, t.Code, t.NameAr, t.NameEn, t.IconUrl, t.SortOrder })
                .ToListAsync(ct);

            IReadOnlyList<MaintenanceServiceTypeDto> items = rows.Select(t => new MaintenanceServiceTypeDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = _language.Pick(t.NameAr, t.NameEn),
                IconUrl = t.IconUrl,
                SortOrder = t.SortOrder
            }).ToList();

            return Result<IReadOnlyList<MaintenanceServiceTypeDto>>.Ok(items);
        }

        public async Task<Result<IReadOnlyList<MaintenanceServiceTypeAdminDto>>> ListForAdminAsync(
            AccountType activeRole, CancellationToken ct)
        {
            if (activeRole != AccountType.Admin)
                return Result<IReadOnlyList<MaintenanceServiceTypeAdminDto>>.Fail(
                    AppErrorCode.Forbidden, "Switch to your admin account to manage the catalogue.");

            var items = await _db.MaintenanceServiceTypes
                .AsNoTracking()
                .OrderBy(t => t.SortOrder)
                .Select(t => new MaintenanceServiceTypeAdminDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    NameAr = t.NameAr,
                    NameEn = t.NameEn,
                    IconUrl = t.IconUrl,
                    SortOrder = t.SortOrder,
                    IsActive = t.IsActive,
                    RequestsCount = _db.MaintenanceRequests.Count(m => m.ServiceTypeId == t.Id),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync(ct);

            // No Pick here: an admin editing a row needs both languages, not the one they are reading.
            return Result<IReadOnlyList<MaintenanceServiceTypeAdminDto>>.Ok(items);
        }

        public async Task<Result<MaintenanceServiceTypeAdminDto>> CreateAsync(
            CreateMaintenanceServiceTypeDto dto, AccountType activeRole, CancellationToken ct)
        {
            if (activeRole != AccountType.Admin)
                return Result<MaintenanceServiceTypeAdminDto>.Fail(
                    AppErrorCode.Forbidden, "Switch to your admin account to manage the catalogue.");

            var code = dto.Code.Trim();

            // Checked here rather than in the validator because it needs the database. The unique
            // index enforces it too; this turns a constraint violation into a 409.
            if (await _db.MaintenanceServiceTypes.AnyAsync(t => t.Code == code, ct))
                return Result<MaintenanceServiceTypeAdminDto>.Fail(
                    AppErrorCode.Conflict, $"A service type with the code '{code}' already exists.");

            var now = _time.GetUtcNow();

            // Default to the end of the list rather than 0, which would put a new category first.
            var sortOrder = dto.SortOrder
                ?? await _db.MaintenanceServiceTypes
                    .Select(t => (int?)t.SortOrder)
                    .MaxAsync(ct) + 10
                ?? 10;

            var entity = new MaintenanceServiceType
            {
                Id = Guid.NewGuid(),
                Code = code,
                NameAr = dto.NameAr.Trim(),
                NameEn = dto.NameEn.Trim(),
                IconUrl = string.IsNullOrWhiteSpace(dto.IconUrl) ? null : dto.IconUrl.Trim(),
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.MaintenanceServiceTypes.Add(entity);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Service type {Code} created ({Id}).", entity.Code, entity.Id);

            return Result<MaintenanceServiceTypeAdminDto>.Ok(Map(entity, requestsCount: 0));
        }

        public async Task<Result<MaintenanceServiceTypeAdminDto>> UpdateAsync(
            Guid id, UpdateMaintenanceServiceTypeDto dto, AccountType activeRole, CancellationToken ct)
        {
            if (activeRole != AccountType.Admin)
                return Result<MaintenanceServiceTypeAdminDto>.Fail(
                    AppErrorCode.Forbidden, "Switch to your admin account to manage the catalogue.");

            var entity = await _db.MaintenanceServiceTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (entity is null)
                return Result<MaintenanceServiceTypeAdminDto>.Fail(
                    AppErrorCode.NotFound, "Service type not found.");

            if (dto.NameAr is not null) entity.NameAr = dto.NameAr.Trim();
            if (dto.NameEn is not null) entity.NameEn = dto.NameEn.Trim();
            if (dto.SortOrder is { } order) entity.SortOrder = order;
            if (dto.IsActive is { } active) entity.IsActive = active;

            // "" clears it, omitted leaves it — the same shape as the property patch endpoints.
            if (dto.IconUrl is not null)
                entity.IconUrl = string.IsNullOrWhiteSpace(dto.IconUrl) ? null : dto.IconUrl.Trim();

            entity.UpdatedAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct);

            var requestsCount = await _db.MaintenanceRequests.CountAsync(m => m.ServiceTypeId == id, ct);

            _logger.LogInformation("Service type {Code} updated ({Id}).", entity.Code, entity.Id);

            return Result<MaintenanceServiceTypeAdminDto>.Ok(Map(entity, requestsCount));
        }

        // Deactivate, never delete. A category with requests against it cannot be removed — the FK
        // forbids it — and one without is still safer hidden than gone, since the client may already
        // have mapped an icon to its code.
        public async Task<Result<bool>> DeactivateAsync(Guid id, AccountType activeRole, CancellationToken ct)
        {
            if (activeRole != AccountType.Admin)
                return Result<bool>.Fail(
                    AppErrorCode.Forbidden, "Switch to your admin account to manage the catalogue.");

            var entity = await _db.MaintenanceServiceTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (entity is null)
                return Result<bool>.Fail(AppErrorCode.NotFound, "Service type not found.");

            if (!entity.IsActive)
                return Result<bool>.Ok(true);

            // The last active category would leave the tenant unable to raise anything at all.
            var activeCount = await _db.MaintenanceServiceTypes.CountAsync(t => t.IsActive, ct);
            if (activeCount <= 1)
                return Result<bool>.Fail(AppErrorCode.Conflict,
                    "This is the only active service type; add another before deactivating it.");

            entity.IsActive = false;
            entity.UpdatedAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Service type {Code} deactivated ({Id}).", entity.Code, entity.Id);

            return Result<bool>.Ok(true);
        }

        private static MaintenanceServiceTypeAdminDto Map(MaintenanceServiceType t, int requestsCount) => new()
        {
            Id = t.Id,
            Code = t.Code,
            NameAr = t.NameAr,
            NameEn = t.NameEn,
            IconUrl = t.IconUrl,
            SortOrder = t.SortOrder,
            IsActive = t.IsActive,
            RequestsCount = requestsCount,
            CreatedAt = t.CreatedAt
        };
    }


}
