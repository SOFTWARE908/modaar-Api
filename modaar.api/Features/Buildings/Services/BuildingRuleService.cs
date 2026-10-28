using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Buildings.Dtos;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Buildings.Services;

public interface IBuildingRuleService
{
    Task<Result<BuildingRulesDto>> GetForBuildingAsync(Guid buildingId, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BuildingRulesDto>> GetForPropertyAsync(Guid propertyId, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BuildingRulesDto>> AddRuleAsync(Guid buildingId, Guid callerUserId, AccountType role, AddBuildingRuleDto request, CancellationToken ct);
    Task<Result<BuildingRulesDto>> DeleteRuleAsync(Guid buildingId, Guid ruleId, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BuildingRulesDto>> AddContactAsync(Guid buildingId, Guid callerUserId, AccountType role, AddBuildingContactDto request, CancellationToken ct);
    Task<Result<BuildingRulesDto>> DeleteContactAsync(Guid buildingId, Guid contactId, Guid callerUserId, AccountType role, CancellationToken ct);

    // Categories: everyone reads the active ones; the admin manages them.
    Task<List<BuildingRuleCategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<Result<List<BuildingRuleCategoryAdminDto>>> ListCategoriesForAdminAsync(AccountType role, CancellationToken ct);
    Task<Result<BuildingRuleCategoryAdminDto>> CreateCategoryAsync(AccountType role, CreateBuildingRuleCategoryDto request, CancellationToken ct);
    Task<Result<BuildingRuleCategoryAdminDto>> UpdateCategoryAsync(Guid id, AccountType role, UpdateBuildingRuleCategoryDto request, CancellationToken ct);
    Task<Result<bool>> DeleteCategoryAsync(Guid id, AccountType role, CancellationToken ct);
}

// Building rules ("أنظمة المبنى"). The categories (sections) are one admin-managed list for every
// building; each building's rules sit under them. Who reads a building's rules:
//   owner  → buildings holding a unit they own, or that they added
//   broker → buildings holding a unit their brokerage manages, or that they added
//   tenant → the building of a unit they hold an active lease on (by propertyId)
// Only whoever added the building writes them.
public sealed class BuildingRuleService : IBuildingRuleService
{
    private const int MaxRulesPerCategory = 30;
    private const int MaxContacts = 10;
    private const string AdminOnly = "Switch to your admin account to manage building rule categories.";

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;

    public BuildingRuleService(ModaarDbContext db, TimeProvider time, IRequestLanguage language)
    {
        _db = db;
        _time = time;
        _language = language;
    }

    public async Task<Result<BuildingRulesDto>> GetForBuildingAsync(
        Guid buildingId, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        var building = await _db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null || !await CanSeeAsync(building, callerUserId, role, ct))
            return Result<BuildingRulesDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        return Result<BuildingRulesDto>.Ok(await ToDtoAsync(building, callerUserId, ct));
    }

    public async Task<Result<BuildingRulesDto>> GetForPropertyAsync(
        Guid propertyId, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        var property = await _db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId && !p.IsDeleted)
            .Select(p => new { p.BuildingId, p.OwnerUserId, BrokerUserId = (Guid?)p.Broker!.UserId })
            .FirstOrDefaultAsync(ct);

        var allowed = property is not null && role switch
        {
            AccountType.Tenant => await _db.Contracts.AnyAsync(c => c.PropertyId == propertyId
                && c.TenantUserId == callerUserId && c.Status == ContractStatus.Active, ct),
            AccountType.Owner => property.OwnerUserId == callerUserId,
            AccountType.Broker => property.BrokerUserId == callerUserId,
            _ => false
        };
        if (!allowed)
            return Result<BuildingRulesDto>.Fail(AppErrorCode.NotFound, "Property not found.");

        // Rules belong to a building; a unit outside one has none.
        var building = property!.BuildingId is { } buildingId
            ? await _db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct)
            : null;
        if (building is null)
            return Result<BuildingRulesDto>.Fail(AppErrorCode.NotFound, "This unit is not in a building.");

        return Result<BuildingRulesDto>.Ok(await ToDtoAsync(building, callerUserId, ct));
    }

    public Task<Result<BuildingRulesDto>> AddRuleAsync(
        Guid buildingId, Guid callerUserId, AccountType role, AddBuildingRuleDto request, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async () =>
        {
            if (!await _db.BuildingRuleCategories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct))
                return new Failure(AppErrorCode.NotFound, "Category not found.");

            var orders = await _db.BuildingRules
                .Where(r => r.BuildingId == buildingId && r.CategoryId == request.CategoryId)
                .Select(r => r.SortOrder)
                .ToListAsync(ct);
            if (orders.Count >= MaxRulesPerCategory)
                return new Failure(AppErrorCode.Conflict, $"A section can have at most {MaxRulesPerCategory} rules.");

            _db.BuildingRules.Add(new BuildingRule
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                CategoryId = request.CategoryId,
                Text = request.Text.Trim(),
                SortOrder = orders.Count == 0 ? 0 : orders.Max() + 1
            });
            return null;
        }, ct);

    public Task<Result<BuildingRulesDto>> DeleteRuleAsync(
        Guid buildingId, Guid ruleId, Guid callerUserId, AccountType role, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async () =>
        {
            var rule = await _db.BuildingRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.BuildingId == buildingId, ct);
            if (rule is null)
                return new Failure(AppErrorCode.NotFound, "Rule not found.");

            _db.BuildingRules.Remove(rule);
            return null;
        }, ct);

    public Task<Result<BuildingRulesDto>> AddContactAsync(
        Guid buildingId, Guid callerUserId, AccountType role, AddBuildingContactDto request, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async () =>
        {
            var orders = await _db.BuildingContacts.Where(c => c.BuildingId == buildingId).Select(c => c.SortOrder).ToListAsync(ct);
            if (orders.Count >= MaxContacts)
                return new Failure(AppErrorCode.Conflict, $"A building can have at most {MaxContacts} numbers.");

            _db.BuildingContacts.Add(new BuildingContact
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                Name = request.Name.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                IsEmergency = request.IsEmergency,
                SortOrder = orders.Count == 0 ? 0 : orders.Max() + 1
            });
            return null;
        }, ct);

    public Task<Result<BuildingRulesDto>> DeleteContactAsync(
        Guid buildingId, Guid contactId, Guid callerUserId, AccountType role, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async () =>
        {
            var contact = await _db.BuildingContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.BuildingId == buildingId, ct);
            if (contact is null)
                return new Failure(AppErrorCode.NotFound, "Number not found.");

            _db.BuildingContacts.Remove(contact);
            return null;
        }, ct);

    // ── Categories ─────────────────────────────────────────────────────────

    public async Task<List<BuildingRuleCategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        (await _db.BuildingRuleCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync(ct))
        .Select(c => new BuildingRuleCategoryDto { Id = c.Id, Code = c.Code, Name = _language.Pick(c.NameAr, c.NameEn), IconUrl = c.IconUrl })
        .ToList();

    public async Task<Result<List<BuildingRuleCategoryAdminDto>>> ListCategoriesForAdminAsync(AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<List<BuildingRuleCategoryAdminDto>>.Fail(AppErrorCode.Forbidden, AdminOnly);

        return Result<List<BuildingRuleCategoryAdminDto>>.Ok((await _db.BuildingRuleCategories.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
                .ToListAsync(ct))
            .Select(Admin)
            .ToList());
    }

    public async Task<Result<BuildingRuleCategoryAdminDto>> CreateCategoryAsync(
        AccountType role, CreateBuildingRuleCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<BuildingRuleCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var code = request.Code.Trim();
        if (await _db.BuildingRuleCategories.AnyAsync(c => c.Code == code, ct))
            return Result<BuildingRuleCategoryAdminDto>.Fail(AppErrorCode.Conflict, $"A category with the code '{code}' already exists.");

        var now = _time.GetUtcNow();
        var entity = new BuildingRuleCategory
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
        _db.BuildingRuleCategories.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Result<BuildingRuleCategoryAdminDto>.Ok(Admin(entity));
    }

    public async Task<Result<BuildingRuleCategoryAdminDto>> UpdateCategoryAsync(
        Guid id, AccountType role, UpdateBuildingRuleCategoryDto request, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<BuildingRuleCategoryAdminDto>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.BuildingRuleCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<BuildingRuleCategoryAdminDto>.Fail(AppErrorCode.NotFound, "Category not found.");

        if (request.NameAr is not null) entity.NameAr = request.NameAr.Trim();
        if (request.NameEn is not null) entity.NameEn = request.NameEn.Trim();
        if (request.IconUrl is not null) entity.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        if (request.SortOrder is { } order) entity.SortOrder = order;
        if (request.IsActive is { } active) entity.IsActive = active;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return Result<BuildingRuleCategoryAdminDto>.Ok(Admin(entity));
    }

    // Deactivates: rules already written under it keep showing, but no new ones go in.
    public async Task<Result<bool>> DeleteCategoryAsync(Guid id, AccountType role, CancellationToken ct)
    {
        if (role != AccountType.Admin)
            return Result<bool>.Fail(AppErrorCode.Forbidden, AdminOnly);

        var entity = await _db.BuildingRuleCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Category not found.");

        entity.IsActive = false;
        entity.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    // ── Shared ─────────────────────────────────────────────────────────────

    private sealed record Failure(AppErrorCode Code, string Message);

    // Every change: the building must be visible, only its creator edits, and "آخر تحديث" moves.
    // Returns the whole rules screen so the app can redraw from it.
    private async Task<Result<BuildingRulesDto>> EditAsync(
        Guid buildingId, Guid callerUserId, AccountType role, Func<Task<Failure?>> change, CancellationToken ct)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null || !await CanSeeAsync(building, callerUserId, role, ct))
            return Result<BuildingRulesDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        if (building.CreatedByUserId != callerUserId)
            return Result<BuildingRulesDto>.Fail(AppErrorCode.Forbidden, "Only whoever added this building can change its rules.");

        if (await change() is { } failure)
            return Result<BuildingRulesDto>.Fail(failure.Code, failure.Message);

        building.RulesUpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return Result<BuildingRulesDto>.Ok(await ToDtoAsync(building, callerUserId, ct));
    }

    private async Task<bool> CanSeeAsync(Building building, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (building.CreatedByUserId == callerUserId) return true;

        var units = _db.Properties.Where(p => !p.IsDeleted && p.BuildingId == building.Id);
        return role switch
        {
            AccountType.Owner => await units.AnyAsync(p => p.OwnerUserId == callerUserId, ct),
            AccountType.Broker => await units.AnyAsync(p => p.Broker!.UserId == callerUserId, ct),
            _ => false
        };
    }

    private async Task<BuildingRulesDto> ToDtoAsync(Building building, Guid callerUserId, CancellationToken ct)
    {
        var rules = await _db.BuildingRules.AsNoTracking()
            .Where(r => r.BuildingId == building.Id)
            .Join(_db.BuildingRuleCategories, r => r.CategoryId, c => c.Id, (r, c) => new { Rule = r, Category = c })
            .ToListAsync(ct);

        var sections = rules
            .GroupBy(x => x.Category.Id)
            .Select(g => g.First().Category)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(c => new BuildingRuleSectionDto
            {
                CategoryId = c.Id,
                Code = c.Code,
                Name = _language.Pick(c.NameAr, c.NameEn),
                IconUrl = c.IconUrl,
                Rules = rules.Where(x => x.Category.Id == c.Id)
                    .OrderBy(x => x.Rule.SortOrder)
                    .Select(x => new BuildingRuleDto { Id = x.Rule.Id, Text = x.Rule.Text })
                    .ToList()
            })
            .ToList();

        var contacts = await _db.BuildingContacts.AsNoTracking()
            .Where(c => c.BuildingId == building.Id)
            .OrderBy(c => c.SortOrder)
            .Select(c => new BuildingContactDto
            {
                Id = c.Id,
                Name = c.Name,
                Note = c.Note,
                PhoneNumber = c.PhoneNumber,
                IsEmergency = c.IsEmergency
            })
            .ToListAsync(ct);

        return new BuildingRulesDto
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            UpdatedAt = building.RulesUpdatedAt,
            CanEdit = building.CreatedByUserId == callerUserId,
            Sections = sections,
            Contacts = contacts
        };
    }

    private static BuildingRuleCategoryAdminDto Admin(BuildingRuleCategory c) => new()
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
