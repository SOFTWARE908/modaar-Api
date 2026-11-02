using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

// Everyone reads the active services; the admin manages them. Delete = deactivate: brokers who
// chose it keep it on their profile, but nobody can pick it any more and it drops out of the filter.
public interface IBrokerServiceCategoryService
{
    Task<List<BrokerServiceCategoryDto>> ListAsync(CancellationToken ct);
    Task<Result<List<BrokerServiceCategoryAdminDto>>> ListForAdminAsync(AccountType role, CancellationToken ct);
    Task<Result<BrokerServiceCategoryAdminDto>> CreateAsync(AccountType role, CreateBrokerServiceCategoryDto request, CancellationToken ct);
    Task<Result<BrokerServiceCategoryAdminDto>> UpdateAsync(Guid id, AccountType role, UpdateBrokerServiceCategoryDto request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(Guid id, AccountType role, CancellationToken ct);
}

public sealed class BrokerServiceCategoryService : IBrokerServiceCategoryService
{
    private const string AdminOnly = "Only an admin manages broker services.";

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;

    public BrokerServiceCategoryService(ModaarDbContext db, TimeProvider time, IRequestLanguage language)
    {
        _db = db;
        _time = time;
        _language = language;
    }

    public async Task<List<BrokerServiceCategoryDto>> ListAsync(CancellationToken ct) =>
        (await _db.BrokerServiceCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync(ct))
        .Select(c => new BrokerServiceCategoryDto { Id = c.Id, Code = c.Code, Name = _language.Pick(c.NameAr, c.NameEn), IconUrl = c.IconUrl })
        .ToList();

    public async Task<Result<List<BrokerServiceCategoryAdminDto>>> ListForAdminAsync(AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<List<BrokerServiceCategoryAdminDto>>.Fail(AppErrorCode.Forbidden, AdminOnly);

        return Result<List<BrokerServiceCategoryAdminDto>>.Ok((await _db.BrokerServiceCategories.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
                .ToListAsync(ct))
            .Select(Admin)
            .ToList());
    }

    public async Task<Result<BrokerServiceCategoryAdminDto>> CreateAsync(
        AccountType role, CreateBrokerServiceCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<BrokerServiceCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var code = request.Code.Trim();
        if (await _db.BrokerServiceCategories.AnyAsync(c => c.Code == code, ct))
            return Result<BrokerServiceCategoryAdminDto>.Fail(AppErrorCode.Conflict, $"A service with the code '{code}' already exists.");

        var now = _time.GetUtcNow();
        var entity = new BrokerServiceCategory
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.BrokerServiceCategories.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Result<BrokerServiceCategoryAdminDto>.Ok(Admin(entity));
    }

    public async Task<Result<BrokerServiceCategoryAdminDto>> UpdateAsync(
        Guid id, AccountType role, UpdateBrokerServiceCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<BrokerServiceCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.BrokerServiceCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<BrokerServiceCategoryAdminDto>.Fail(AppErrorCode.NotFound, "Service not found.");

        if (request.NameAr is not null) entity.NameAr = request.NameAr.Trim();
        if (request.NameEn is not null) entity.NameEn = request.NameEn.Trim();
        if (request.IconUrl is not null) entity.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        if (request.SortOrder is { } order) entity.SortOrder = order;
        if (request.IsActive is { } active) entity.IsActive = active;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return Result<BrokerServiceCategoryAdminDto>.Ok(Admin(entity));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<bool>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.BrokerServiceCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Service not found.");

        entity.IsActive = false;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private static BrokerServiceCategoryAdminDto Admin(BrokerServiceCategory c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameAr = c.NameAr,
        NameEn = c.NameEn,
        IconUrl = c.IconUrl,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive
    };
}
