using System.Globalization;
using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Announcements.Dtos;
using modaar.api.Features.Announcements.Entities;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Announcements.Services;

public interface IAnnouncementService
{
    // Owner.
    Task<Result<SentAnnouncementDetailsDto>> SendAsync(Guid callerUserId, AccountType role, SendAnnouncementDto request, CancellationToken ct);
    Task<Result<PagedResultDto<SentAnnouncementDto>>> ListSentAsync(Guid callerUserId, AccountType role, PagedRequestDto query, CancellationToken ct);
    Task<Result<SentAnnouncementDetailsDto>> GetSentAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);

    // Tenant.
    Task<Result<PagedResultDto<AnnouncementDto>>> ListMineAsync(Guid callerUserId, AccountType role, PagedRequestDto query, CancellationToken ct);
    Task<Result<AnnouncementDto>> GetMineAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<AnnouncementDto>> AcknowledgeAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);

    // Categories: everyone reads the active ones; the admin manages them.
    Task<List<AnnouncementCategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<Result<List<AnnouncementCategoryAdminDto>>> ListCategoriesForAdminAsync(AccountType role, CancellationToken ct);
    Task<Result<AnnouncementCategoryAdminDto>> CreateCategoryAsync(AccountType role, CreateAnnouncementCategoryDto request, CancellationToken ct);
    Task<Result<AnnouncementCategoryAdminDto>> UpdateCategoryAsync(Guid id, AccountType role, UpdateAnnouncementCategoryDto request, CancellationToken ct);
    Task<Result<bool>> DeleteCategoryAsync(Guid id, AccountType role, CancellationToken ct);
}

// An owner's notice to their own tenants. Only the owner sends — not a broker, even on units
// the broker runs — and only to tenants with an active lease on the owner's units in scope.
public sealed class AnnouncementService : IAnnouncementService
{
    // ar-EG, not ar-SA: ar-SA formats in the Hijri calendar, and these dates are Gregorian.
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    private readonly ModaarDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IRequestLanguage _language;
    private readonly TimeProvider _time;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(ModaarDbContext db, INotificationService notifications, IRequestLanguage language,
        TimeProvider time, ILogger<AnnouncementService> logger)
    {
        _db = db;
        _notifications = notifications;
        _language = language;
        _time = time;
        _logger = logger;
    }

    // ── Owner ──────────────────────────────────────────────────────────────

    public async Task<Result<SentAnnouncementDetailsDto>> SendAsync(
        Guid callerUserId, AccountType role, SendAnnouncementDto request, CancellationToken ct)
    {
        if (role != AccountType.Owner)
            return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.Forbidden, "Only an owner sends announcements.");

        var category = await _db.AnnouncementCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, ct);
        if (category is null)
            return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.ValidationFailed, "Unknown announcement category.");

        var mine = _db.Properties.Where(p => p.OwnerUserId == callerUserId && !p.IsDeleted);
        List<Guid> unitIds;
        switch (request.Scope)
        {
            case AnnouncementScope.Building:
                unitIds = await mine.Where(p => p.BuildingId == request.BuildingId).Select(p => p.Id).ToListAsync(ct);
                // Not their building and no units of theirs in it read the same.
                if (unitIds.Count == 0)
                    return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.NotFound, "Building not found.");
                break;

            case AnnouncementScope.Units:
                var asked = request.PropertyIds!.Distinct().ToList();
                unitIds = await mine.Where(p => asked.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
                if (unitIds.Count != asked.Count)
                    return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.ValidationFailed, "One or more units are unknown or not yours.");
                break;

            default:
                unitIds = await mine.Select(p => p.Id).ToListAsync(ct);
                break;
        }

        // One row per tenant, tied to their first unit in scope.
        var tenants = (await _db.Contracts
                .AsNoTracking()
                .Where(c => unitIds.Contains(c.PropertyId) && c.Status == ContractStatus.Active)
                .Select(c => new { c.TenantUserId, c.PropertyId })
                .ToListAsync(ct))
            .GroupBy(c => c.TenantUserId)
            .Select(g => g.First())
            .ToList();

        if (tenants.Count == 0)
            return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.Conflict, "No tenants with an active lease in these units.");

        var now = _time.GetUtcNow();
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            OwnerUserId = callerUserId,
            CategoryId = category.Id,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            ImageUrl = request.ImageUrl?.Trim(),
            Scope = request.Scope,
            BuildingId = request.Scope == AnnouncementScope.Building ? request.BuildingId : null,
            PropertyIds = request.Scope == AnnouncementScope.Units ? unitIds : [],
            CreatedAt = now,
            Recipients = tenants.Select(t => new AnnouncementRecipient
            {
                Id = Guid.NewGuid(),
                TenantUserId = t.TenantUserId,
                PropertyId = t.PropertyId
            }).ToList()
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Announcement {Id} ({Category}) from owner {OwnerId} to {Count} tenant(s).",
            announcement.Id, category.Code, callerUserId, tenants.Count);

        await _notifications.NotifyAsync(tenants.Select(t => t.TenantUserId), Notification(announcement, category), ct);

        return Result<SentAnnouncementDetailsDto>.Ok((await SentAsync(_db.Announcements.Where(a => a.Id == announcement.Id), withRecipients: true, ct)).Single());
    }

    public async Task<Result<PagedResultDto<SentAnnouncementDto>>> ListSentAsync(
        Guid callerUserId, AccountType role, PagedRequestDto query, CancellationToken ct)
    {
        if (role != AccountType.Owner)
            return Result<PagedResultDto<SentAnnouncementDto>>.Fail(AppErrorCode.Forbidden, "Only an owner sends announcements.");

        var rows = _db.Announcements.Where(a => a.OwnerUserId == callerUserId);
        var total = await rows.CountAsync(ct);
        var page = await SentAsync(rows.OrderByDescending(a => a.CreatedAt).Skip(query.SkipCount).Take(query.MaxResultCount), withRecipients: false, ct);

        return Result<PagedResultDto<SentAnnouncementDto>>.Ok(new PagedResultDto<SentAnnouncementDto>
        {
            TotalCount = total,
            Items = page
        });
    }

    public async Task<Result<SentAnnouncementDetailsDto>> GetSentAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Owner)
            return Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.Forbidden, "Only an owner sends announcements.");

        var found = await SentAsync(_db.Announcements.Where(a => a.Id == id && a.OwnerUserId == callerUserId), withRecipients: true, ct);
        return found.Count == 0
            ? Result<SentAnnouncementDetailsDto>.Fail(AppErrorCode.NotFound, "Announcement not found.")
            : Result<SentAnnouncementDetailsDto>.Ok(found[0]);
    }

    // ── Tenant ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResultDto<AnnouncementDto>>> ListMineAsync(
        Guid callerUserId, AccountType role, PagedRequestDto query, CancellationToken ct)
    {
        if (role != AccountType.Tenant)
            return Result<PagedResultDto<AnnouncementDto>>.Fail(AppErrorCode.Forbidden, "Announcements are for tenants.");

        var rows = _db.AnnouncementRecipients.Where(r => r.TenantUserId == callerUserId);
        var total = await rows.CountAsync(ct);
        var page = await MineAsync(rows, query.SkipCount, query.MaxResultCount, ct);

        return Result<PagedResultDto<AnnouncementDto>>.Ok(new PagedResultDto<AnnouncementDto> { TotalCount = total, Items = page });
    }

    public async Task<Result<AnnouncementDto>> GetMineAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Tenant)
            return Result<AnnouncementDto>.Fail(AppErrorCode.Forbidden, "Announcements are for tenants.");

        var found = await MineAsync(_db.AnnouncementRecipients.Where(r => r.TenantUserId == callerUserId && r.AnnouncementId == id), 0, 1, ct);
        return found.Count == 0
            ? Result<AnnouncementDto>.Fail(AppErrorCode.NotFound, "Announcement not found.")
            : Result<AnnouncementDto>.Ok(found[0]);
    }

    // "تم الاطلاع". Repeating it keeps the first time.
    public async Task<Result<AnnouncementDto>> AcknowledgeAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Tenant)
            return Result<AnnouncementDto>.Fail(AppErrorCode.Forbidden, "Announcements are for tenants.");

        var row = await _db.AnnouncementRecipients.FirstOrDefaultAsync(r => r.AnnouncementId == id && r.TenantUserId == callerUserId, ct);
        if (row is null)
            return Result<AnnouncementDto>.Fail(AppErrorCode.NotFound, "Announcement not found.");

        if (row.AcknowledgedAt is null)
        {
            row.AcknowledgedAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct);
        }

        return await GetMineAsync(id, callerUserId, role, ct);
    }

    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<List<AnnouncementCategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        (await _db.AnnouncementCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync(ct))
        .Select(Category)
        .ToList();

    public async Task<Result<List<AnnouncementCategoryAdminDto>>> ListCategoriesForAdminAsync(AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<List<AnnouncementCategoryAdminDto>>.Fail(AppErrorCode.Forbidden, AdminOnly);

        return Result<List<AnnouncementCategoryAdminDto>>.Ok((await _db.AnnouncementCategories.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
                .ToListAsync(ct))
            .Select(Admin)
            .ToList());
    }

    public async Task<Result<AnnouncementCategoryAdminDto>> CreateCategoryAsync(
        AccountType role, CreateAnnouncementCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<AnnouncementCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var code = request.Code.Trim();
        if (await _db.AnnouncementCategories.AnyAsync(c => c.Code == code, ct))
            return Result<AnnouncementCategoryAdminDto>.Fail(AppErrorCode.Conflict, $"A category with the code '{code}' already exists.");

        var now = _time.GetUtcNow();
        var entity = new AnnouncementCategory
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.AnnouncementCategories.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Announcement category {Code} created ({Id}).", entity.Code, entity.Id);
        return Result<AnnouncementCategoryAdminDto>.Ok(Admin(entity));
    }

    public async Task<Result<AnnouncementCategoryAdminDto>> UpdateCategoryAsync(
        Guid id, AccountType role, UpdateAnnouncementCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<AnnouncementCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.AnnouncementCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<AnnouncementCategoryAdminDto>.Fail(AppErrorCode.NotFound, "Category not found.");

        if (request.NameAr is not null) entity.NameAr = request.NameAr.Trim();
        if (request.NameEn is not null) entity.NameEn = request.NameEn.Trim();
        if (request.SortOrder is { } order) entity.SortOrder = order;
        if (request.IsActive is { } active) entity.IsActive = active;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return Result<AnnouncementCategoryAdminDto>.Ok(Admin(entity));
    }

    // Deactivates: announcements already sent keep showing their category.
    public async Task<Result<bool>> DeleteCategoryAsync(Guid id, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<bool>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.AnnouncementCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Category not found.");

        if (entity.IsActive && await _db.AnnouncementCategories.CountAsync(c => c.IsActive, ct) == 1)
            return Result<bool>.Fail(AppErrorCode.Conflict, "That is the last active category; owners need at least one.");

        entity.IsActive = false;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private const string AdminOnly = "Switch to your admin account to manage announcement categories.";

    // ── Shared ─────────────────────────────────────────────────────────────

    private async Task<List<SentAnnouncementDetailsDto>> SentAsync(IQueryable<Announcement> rows, bool withRecipients, CancellationToken ct)
    {
        var page = await rows
            .AsNoTracking()
            .Select(a => new
            {
                a.Id, a.CategoryId, a.Title, a.Body, a.StartsAt, a.EndsAt, a.ImageUrl, a.Scope, a.BuildingId, a.PropertyIds, a.CreatedAt,
                BuildingName = _db.Buildings.Where(b => b.Id == a.BuildingId).Select(b => b.Name).FirstOrDefault(),
                Recipients = a.Recipients.Count,
                Acknowledged = a.Recipients.Count(r => r.AcknowledgedAt != null),
                OwnerUnits = _db.Properties.Count(p => p.OwnerUserId == a.OwnerUserId && !p.IsDeleted
                                                       && (a.BuildingId == null || p.BuildingId == a.BuildingId))
            })
            .ToListAsync(ct);

        var categories = await CategoriesAsync(page.Select(a => a.CategoryId), ct);

        var ids = page.Select(a => a.Id).ToList();
        var recipients = withRecipients
            ? (await _db.AnnouncementRecipients.AsNoTracking()
                .Where(r => ids.Contains(r.AnnouncementId))
                .Select(r => new
                {
                    r.AnnouncementId,
                    Dto = new AnnouncementRecipientDto
                    {
                        TenantUserId = r.TenantUserId,
                        Name = _db.Users.Where(u => u.Id == r.TenantUserId).Select(u => u.FullName).FirstOrDefault(),
                        ImageUrl = _db.Users.Where(u => u.Id == r.TenantUserId).Select(u => u.ProfileImageUrl).FirstOrDefault(),
                        PropertyTitle = _db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Title).FirstOrDefault() ?? string.Empty,
                        UnitNumber = _db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.UnitNumber).FirstOrDefault(),
                        AcknowledgedAt = r.AcknowledgedAt
                    }
                })
                .ToListAsync(ct))
              .ToLookup(r => r.AnnouncementId, r => r.Dto)
            : null;

        return page.Select(a => new SentAnnouncementDetailsDto
        {
            Id = a.Id,
            Category = categories[a.CategoryId],
            Title = a.Title,
            Body = a.Body,
            StartsAt = a.StartsAt,
            EndsAt = a.EndsAt,
            ImageUrl = a.ImageUrl,
            Scope = a.Scope,
            BuildingId = a.BuildingId,
            BuildingName = a.BuildingName,
            UnitsCount = a.Scope == AnnouncementScope.Units ? a.PropertyIds.Count : a.OwnerUnits,
            RecipientsCount = a.Recipients,
            AcknowledgedCount = a.Acknowledged,
            CreatedAt = a.CreatedAt,
            Recipients = recipients is null
                ? []
                : recipients[a.Id].OrderBy(r => r.AcknowledgedAt is null).ThenBy(r => r.Name).ToList()
        }).ToList();
    }

    private async Task<List<AnnouncementDto>> MineAsync(IQueryable<AnnouncementRecipient> rows, int skip, int take, CancellationToken ct)
    {
        var page = await rows
            .AsNoTracking()
            .Join(_db.Announcements, r => r.AnnouncementId, a => a.Id, (r, a) => new { r, a })
            .OrderByDescending(x => x.a.CreatedAt)
            .Skip(skip).Take(take)
            .Select(x => new
            {
                x.a.Id, x.a.CategoryId, x.a.Title, x.a.Body, x.a.StartsAt, x.a.EndsAt, x.a.ImageUrl, x.a.CreatedAt,
                x.r.AcknowledgedAt,
                Sender = _db.Users.Where(u => u.Id == x.a.OwnerUserId).Select(u => new { u.FullName, u.ProfileImageUrl }).FirstOrDefault(),
                Unit = _db.Properties.Where(p => p.Id == x.r.PropertyId).Select(p => new
                {
                    p.Title,
                    p.UnitNumber,
                    BuildingName = _db.Buildings.Where(b => b.Id == p.BuildingId).Select(b => b.Name).FirstOrDefault()
                }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var categories = await CategoriesAsync(page.Select(a => a.CategoryId), ct);

        return page.Select(a => new AnnouncementDto
        {
            Id = a.Id,
            Category = categories[a.CategoryId],
            Title = a.Title,
            Body = a.Body,
            StartsAt = a.StartsAt,
            EndsAt = a.EndsAt,
            ImageUrl = a.ImageUrl,
            SenderName = a.Sender?.FullName,
            SenderImageUrl = a.Sender?.ProfileImageUrl,
            PropertyTitle = a.Unit?.Title ?? string.Empty,
            UnitNumber = a.Unit?.UnitNumber,
            BuildingName = a.Unit?.BuildingName,
            AcknowledgedAt = a.AcknowledgedAt,
            CreatedAt = a.CreatedAt
        }).ToList();
    }

    // Inactive ones too: what was sent keeps its category.
    private async Task<Dictionary<Guid, AnnouncementCategoryDto>> CategoriesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        return (await _db.AnnouncementCategories.AsNoTracking().Where(c => wanted.Contains(c.Id)).ToListAsync(ct))
            .ToDictionary(c => c.Id, Category);
    }

    private AnnouncementCategoryDto Category(AnnouncementCategory c) =>
        new() { Id = c.Id, Code = c.Code, Name = _language.Pick(c.NameAr, c.NameEn) };

    private static AnnouncementCategoryAdminDto Admin(AnnouncementCategory c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameAr = c.NameAr,
        NameEn = c.NameEn,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive
    };

    // Title is the owner's own words; the body says what kind and when, in each tenant's language.
    private static NewNotification Notification(Announcement a, AnnouncementCategory c)
    {
        string When(CultureInfo culture, string at)
        {
            if (a.StartsAt is not { } s) return string.Empty;
            var riyadh = TimeZoneInfo.ConvertTime(s, SaudiTime.Zone);
            return $" · {riyadh.ToString("dddd d MMMM", culture)} {at} {riyadh.ToString("h:mm tt", culture)}";
        }

        return new NewNotification(NotificationType.Announcements,
            a.Title, a.Title,
            c.NameAr + When(Arabic, "الساعة"), c.NameEn + When(English, "at"),
            NotificationTargetType.Announcement, a.Id);
    }
}
