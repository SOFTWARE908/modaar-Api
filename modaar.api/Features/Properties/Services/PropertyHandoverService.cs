using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Properties.Services;

public sealed class PropertyHandoverService : IPropertyHandoverService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<PropertyHandoverService> _logger;

    public PropertyHandoverService(
        ModaarDbContext db, TimeProvider time, ILogger<PropertyHandoverService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<PropertyHandoverDto>> GetAsync(
        Guid propertyId, Guid callerUserId, CancellationToken ct)
    {
        var access = await ResolveAsync(propertyId, callerUserId, ct);
        if (access.Failure is { } failure)
            return Result<PropertyHandoverDto>.Fail(failure.ErrorCode!.Value, failure.Error!);

        // The tenant may read the record for their own tenancy, but not one from a previous
        // tenant — that is the previous tenant's evidence, not theirs.
        if (!access.IsOwner && !access.IsManagingBroker && !access.IsCurrentTenant)
            return Result<PropertyHandoverDto>.Fail(
                AppErrorCode.Forbidden, "You do not have access to this property.");

        var handover = await FindForCurrentTenancyAsync(propertyId, access.ActiveContractId, ct);

        return handover is null
            ? Result<PropertyHandoverDto>.Fail(AppErrorCode.NotFound, "No handover record exists.")
            : Result<PropertyHandoverDto>.Ok(ToDto(handover));
    }

    public async Task<Result<PropertyHandoverDto>> SubmitAsync(
        Guid propertyId, Guid callerUserId, SubmitPropertyHandoverDto request, CancellationToken ct)
    {
        var access = await ResolveAsync(propertyId, callerUserId, ct);
        if (access.Failure is { } failure)
            return Result<PropertyHandoverDto>.Fail(failure.ErrorCode!.Value, failure.Error!);

        // The tenant receives the unit; they do not record its condition. That is the handing
        // party's statement.
        if (!access.IsOwner && !access.IsManagingBroker)
            return Result<PropertyHandoverDto>.Fail(
                AppErrorCode.Forbidden, "Only the owner or the managing broker can record a handover.");

        var now = _time.GetUtcNow();
        var handover = await FindForCurrentTenancyAsync(propertyId, access.ActiveContractId, ct, tracked: true);

        if (handover is null)
        {
            handover = new PropertyHandover
            {
                Id = Guid.NewGuid(),
                PropertyId = propertyId,
                ContractId = access.ActiveContractId,
                CreatedAt = now
            };
            _db.PropertyHandovers.Add(handover);
        }

        handover.Images = request.Images
            .Select((url, index) => new PropertyImage { Url = url.Trim(), SortOrder = index })
            .ToList();

        handover.Description = request.Description?.Trim();
        handover.SignatureUrl = request.SignatureUrl?.Trim();

        // Status and date come from the caller, per the spec — the server records them rather
        // than deciding them. A resubmission overwrites, including one that is already complete.
        if (request.Status is { } status)
            handover.Status = status;

        if (request.HandoverDate is { } handoverDate)
            handover.HandoverDate = handoverDate;

        handover.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Handover recorded as {Status} for property {PropertyId} by {UserId}.",
            handover.Status, propertyId, callerUserId);

        return Result<PropertyHandoverDto>.Ok(ToDto(handover));
    }

    // One record per tenancy. With no active lease, the draft that has no contract attached yet
    // is the one being worked on.
    private Task<PropertyHandover?> FindForCurrentTenancyAsync(
        Guid propertyId, Guid? activeContractId, CancellationToken ct, bool tracked = false)
    {
        var query = tracked ? _db.PropertyHandovers : _db.PropertyHandovers.AsNoTracking();

        return activeContractId is { } contractId
            ? query.FirstOrDefaultAsync(h => h.PropertyId == propertyId && h.ContractId == contractId, ct)
            : query.FirstOrDefaultAsync(h => h.PropertyId == propertyId && h.ContractId == null, ct);
    }

    private async Task<HandoverAccess> ResolveAsync(Guid propertyId, Guid callerUserId, CancellationToken ct)
    {
        var property = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == propertyId && !p.IsDeleted)
            .Select(p => new { p.OwnerUserId, p.BrokerId })
            .FirstOrDefaultAsync(ct);

        if (property is null)
            return new HandoverAccess
            {
                Failure = Result<bool>.Fail(AppErrorCode.NotFound, "Property not found.")
            };

        var contract = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.PropertyId == propertyId && c.Status == ContractStatus.Active)
            .Select(c => new { c.Id, c.TenantUserId })
            .FirstOrDefaultAsync(ct);

        var isManagingBroker = property.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        return new HandoverAccess
        {
            ActiveContractId = contract?.Id,
            IsOwner = property.OwnerUserId == callerUserId,
            IsManagingBroker = isManagingBroker,
            IsCurrentTenant = contract is not null && contract.TenantUserId == callerUserId
        };
    }

    private static PropertyHandoverDto ToDto(PropertyHandover h) => new()
    {
        Id = h.Id,
        PropertyId = h.PropertyId,
        ContractId = h.ContractId,
        Images = h.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        Description = h.Description,
        HandoverDate = h.HandoverDate,
        Status = h.Status,
        SignatureUrl = h.SignatureUrl
    };

    private sealed class HandoverAccess
    {
        public Guid? ActiveContractId { get; init; }
        public bool IsOwner { get; init; }
        public bool IsManagingBroker { get; init; }
        public bool IsCurrentTenant { get; init; }
        public Result<bool>? Failure { get; init; }
    }
}
