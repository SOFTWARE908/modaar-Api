using Microsoft.EntityFrameworkCore;
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
    Task<Result<BuildingRulesDto>> AddSectionAsync(Guid buildingId, Guid callerUserId, AccountType role, AddBuildingRuleSectionDto request, CancellationToken ct);
    Task<Result<BuildingRulesDto>> DeleteSectionAsync(Guid buildingId, Guid sectionId, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BuildingRulesDto>> AddRuleAsync(Guid buildingId, Guid sectionId, Guid callerUserId, AccountType role, AddBuildingRuleDto request, CancellationToken ct);
    Task<Result<BuildingRulesDto>> DeleteRuleAsync(Guid buildingId, Guid sectionId, Guid ruleId, Guid callerUserId, AccountType role, CancellationToken ct);
}

// Building rules ("أنظمة المبنى"). Who reads them:
//   owner  → buildings holding a unit they own, or that they added
//   broker → buildings holding a unit their brokerage manages, or that they added
//   tenant → the building of a unit they hold an active lease on (by propertyId)
// Only whoever added the building writes them.
public sealed class BuildingRuleService : IBuildingRuleService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public BuildingRuleService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
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

    public Task<Result<BuildingRulesDto>> AddSectionAsync(
        Guid buildingId, Guid callerUserId, AccountType role, AddBuildingRuleSectionDto request, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async building =>
        {
            var orders = await _db.BuildingRuleSections.Where(s => s.BuildingId == buildingId).Select(s => s.SortOrder).ToListAsync(ct);
            if (orders.Count >= MaxSections)
                return new Failure(AppErrorCode.Conflict, $"A building can have at most {MaxSections} sections.");

            _db.BuildingRuleSections.Add(new BuildingRuleSection
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                Title = request.Title.Trim(),
                SortOrder = orders.Count == 0 ? 0 : orders.Max() + 1
            });
            return null;
        }, ct);

    // Takes its rules with it.
    public Task<Result<BuildingRulesDto>> DeleteSectionAsync(
        Guid buildingId, Guid sectionId, Guid callerUserId, AccountType role, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async building =>
        {
            var section = await _db.BuildingRuleSections.FirstOrDefaultAsync(s => s.Id == sectionId && s.BuildingId == buildingId, ct);
            if (section is null)
                return new Failure(AppErrorCode.NotFound, "Section not found.");

            _db.BuildingRuleSections.Remove(section);
            return null;
        }, ct);

    public Task<Result<BuildingRulesDto>> AddRuleAsync(
        Guid buildingId, Guid sectionId, Guid callerUserId, AccountType role, AddBuildingRuleDto request, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async building =>
        {
            if (!await _db.BuildingRuleSections.AnyAsync(s => s.Id == sectionId && s.BuildingId == buildingId, ct))
                return new Failure(AppErrorCode.NotFound, "Section not found.");

            var orders = await _db.BuildingRules.Where(r => r.SectionId == sectionId).Select(r => r.SortOrder).ToListAsync(ct);
            if (orders.Count >= MaxRulesPerSection)
                return new Failure(AppErrorCode.Conflict, $"A section can have at most {MaxRulesPerSection} rules.");

            _db.BuildingRules.Add(new BuildingRule
            {
                Id = Guid.NewGuid(),
                SectionId = sectionId,
                Text = request.Text.Trim(),
                SortOrder = orders.Count == 0 ? 0 : orders.Max() + 1
            });
            return null;
        }, ct);

    public Task<Result<BuildingRulesDto>> DeleteRuleAsync(
        Guid buildingId, Guid sectionId, Guid ruleId, Guid callerUserId, AccountType role, CancellationToken ct) =>
        EditAsync(buildingId, callerUserId, role, async building =>
        {
            var rule = await _db.BuildingRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.SectionId == sectionId
                && _db.BuildingRuleSections.Any(s => s.Id == sectionId && s.BuildingId == buildingId), ct);
            if (rule is null)
                return new Failure(AppErrorCode.NotFound, "Rule not found.");

            _db.BuildingRules.Remove(rule);
            return null;
        }, ct);

    private const int MaxSections = 20;
    private const int MaxRulesPerSection = 30;

    private sealed record Failure(AppErrorCode Code, string Message);

    // Every change: the building must be visible, only its creator edits, and "آخر تحديث" moves.
    // Returns the whole rules screen so the app can redraw from it.
    private async Task<Result<BuildingRulesDto>> EditAsync(
        Guid buildingId, Guid callerUserId, AccountType role, Func<Building, Task<Failure?>> change, CancellationToken ct)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null || !await CanSeeAsync(building, callerUserId, role, ct))
            return Result<BuildingRulesDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        if (building.CreatedByUserId != callerUserId)
            return Result<BuildingRulesDto>.Fail(AppErrorCode.Forbidden, "Only whoever added this building can change its rules.");

        if (await change(building) is { } failure)
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
        var sections = await _db.BuildingRuleSections.AsNoTracking()
            .Where(s => s.BuildingId == building.Id)
            .OrderBy(s => s.SortOrder)
            .Select(s => new BuildingRuleSectionDto
            {
                Id = s.Id,
                Title = s.Title,
                Rules = s.Rules.OrderBy(r => r.SortOrder).Select(r => new BuildingRuleDto { Id = r.Id, Text = r.Text }).ToList()
            })
            .ToListAsync(ct);

        return new BuildingRulesDto
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            UpdatedAt = building.RulesUpdatedAt,
            CanEdit = building.CreatedByUserId == callerUserId,
            Sections = sections
        };
    }
}
