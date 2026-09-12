using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Properties.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Persistence;

namespace modaar.api.Features.Properties.Services;

public sealed class PropertyService : IPropertyService
{
    private const string DefaultCurrency = "SAR";

    // How close to the end of a lease the renew button lights up.
    private const int RenewalWindowDays = 60;

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public PropertyService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<Result<PagedResultDto<PropertyListItemDto>>> ListForOwnerAsync(
        Guid ownerUserId, PropertyListQueryDto query, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        var properties = _db.Properties
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.OwnerUserId == ownerUserId);

        if (query.PropertyType is { } type)
            properties = properties.Where(p => p.PropertyType == type);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            properties = properties.Where(p =>
                p.Title.Contains(term) ||
                (p.District != null && p.District.Contains(term)) ||
                (p.City != null && p.City.Contains(term)));
        }

        // Rented/vacant is the existence of an active contract, so the filter is a subquery
        // rather than a column comparison.
        if (query.Status is { } status)
        {
            var rented = status == PropertyRentalStatus.Rented;
            properties = properties.Where(p =>
                _db.Contracts.Any(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active) == rented);
        }

        // Project property + its active contract + the tenant's name in one round trip. The
        // contract subquery is covered by the filtered unique index on Contracts.
        var rows = properties.Select(p => new
        {
            p.Id,
            p.Title,
            p.CoverImageUrl,
            p.AnnualRent,
            Contract = _db.Contracts
                .Where(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active)
                .Select(c => new
                {
                    c.MonthlyAmount,
                    c.Currency,
                    c.StartDate,
                    c.EndDate,
                    TenantName = _db.Users
                        .Where(u => u.Id == c.TenantUserId)
                        .Select(u => u.FullName)
                        .FirstOrDefault()
                })
                .FirstOrDefault(),
            p.CreatedAt
        });

        rows = query.Sort switch
        {
            PropertyListSort.TitleAsc    => rows.OrderBy(r => r.Title),
            // Vacant units have no lease end; push them to the back rather than to the front.
            PropertyListSort.LeaseEndAsc => rows
                .OrderBy(r => r.Contract == null)
                .ThenBy(r => r.Contract!.EndDate),
            _                            => rows.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await rows.CountAsync(ct);

        var page = await rows
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        var items = page.Select(r => new PropertyListItemDto
        {
            Id = r.Id,
            Title = r.Title,
            ImageUrl = r.CoverImageUrl,
            Status = r.Contract is null ? PropertyRentalStatus.NotRented : PropertyRentalStatus.Rented,
            AnnualRent = r.AnnualRent,
            Currency = r.Contract?.Currency ?? DefaultCurrency,
            MonthlyRent = r.Contract?.MonthlyAmount,
            TenantName = r.Contract?.TenantName,
            LeaseStart = r.Contract?.StartDate,
            LeaseEnd = r.Contract?.EndDate,
            RentalProgress = r.Contract is null
                ? null
                : LeaseProgress(r.Contract.StartDate, r.Contract.EndDate, today)
        }).ToList();

        return Result<PagedResultDto<PropertyListItemDto>>.Ok(new PagedResultDto<PropertyListItemDto>
        {
            Items = items,
            TotalCount = totalCount
        });
    }

    public async Task<Result<PropertyDetailsDto>> GetDetailsAsync(Guid propertyId, Guid callerUserId, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        var property = await _db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == propertyId && !p.IsDeleted, ct);

        if (property is null)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.NotFound, "Property not found.");

        // The owner, or the brokerage managing it. Deliberately not "any broker".
        var isOwner = property.OwnerUserId == callerUserId;
        var isManagingBroker = property.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        if (!isOwner && !isManagingBroker)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.Forbidden, "You do not have access to this property.");

        var contract = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.PropertyId == propertyId && c.Status == ContractStatus.Active)
            .FirstOrDefaultAsync(ct);

        PropertyTenantContractDto? tenantContract = null;
        if (contract is not null)
        {
            var tenant = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == contract.TenantUserId)
                .Select(u => new { u.FullName, u.ProfileImageUrl })
                .FirstOrDefaultAsync(ct);

            var hasPendingRenewal = await _db.ContractRequests.AnyAsync(r =>
                r.ContractId == contract.Id &&
                r.Type == ContractRequestType.Renewal &&
                r.Status == ContractRequestStatus.Pending, ct);

            var remainingDays = contract.EndDate.DayNumber - today.DayNumber;

            tenantContract = new PropertyTenantContractDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantUserId = contract.TenantUserId,
                TenantName = tenant?.FullName,
                TenantAvatarUrl = tenant?.ProfileImageUrl,
                TenantSubtitle = null,
                Status = contract.Status,
                ContractStart = contract.StartDate,
                ContractEnd = contract.EndDate,
                RemainingDays = remainingDays,
                MonthlyAmount = contract.MonthlyAmount,
                Currency = contract.Currency,
                RenewalAvailability = hasPendingRenewal
                    ? ContractRenewalAvailability.Pending
                    : remainingDays is >= 0 and <= RenewalWindowDays
                        ? ContractRenewalAvailability.Available
                        : ContractRenewalAvailability.Unavailable
            };
        }

        BrokerSummaryDto? broker = null;
        if (property.BrokerId is { } id)
        {
            broker = await _db.Brokers
                .AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new BrokerSummaryDto
                {
                    Id = b.Id,
                    Name = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                    LogoUrl = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault(),
                    Subtitle = b.SubtitleEn,
                    RatingAverage = b.RatingAverage,
                    ReviewsCount = b.ReviewsCount,
                    IsVerified = b.IsVerified
                })
                .FirstOrDefaultAsync(ct);
        }

        // The handover for the current tenancy only — an earlier tenant's record is not this
        // tenant's business.
        var handover = contract is null
            ? null
            : await _db.PropertyHandovers
                .AsNoTracking()
                .Where(h => h.PropertyId == propertyId && h.ContractId == contract.Id)
                .Select(h => new { h.Description, h.Images })
                .FirstOrDefaultAsync(ct);

        var history = await _db.MaintenanceRequests
            .AsNoTracking()
            .Where(m => m.PropertyId == propertyId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .Select(m => new PropertyMaintenanceHistoryItemDto
            {
                RequestId = m.Id,
                RequestNumber = m.RequestNumber,
                Title = m.ServiceType.ToString(),
                Description = m.ProblemDescription,
                ServiceType = m.ServiceType,
                Status = m.Status,
                Date = m.CreatedAt,
                CostAmount = null,
                Currency = null
            })
            .ToListAsync(ct);

        return Result<PropertyDetailsDto>.Ok(new PropertyDetailsDto
        {
            Summary = new PropertySummaryDto
            {
                Id = property.Id,
                Title = property.Title,
                ImageUrl = property.CoverImageUrl,
                Address = ComposeAddress(property.AddressLine, property.District, property.City),
                PropertyType = property.PropertyType,
                RoomsCount = property.RoomsCount,
                AnnualRent = property.AnnualRent,
                Currency = DefaultCurrency,
                Status = contract is null ? PropertyRentalStatus.NotRented : PropertyRentalStatus.Rented,
                Latitude = property.Latitude,
                Longitude = property.Longitude
            },
            TenantContract = tenantContract,
            Broker = broker,
            ImageUrls = property.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            HandoverImageUrls = handover?.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList() ?? [],
            HandoverDescription = handover?.Description,
            MaintenanceHistory = history
        });
    }

    // Clamped to 0–1 so a lease that started early or has run over doesn't drive the progress bar
    // outside its track.
    private static double LeaseProgress(DateOnly start, DateOnly end, DateOnly today)
    {
        var total = end.DayNumber - start.DayNumber;
        if (total <= 0) return 1d;

        var elapsed = today.DayNumber - start.DayNumber;
        return Math.Clamp((double)elapsed / total, 0d, 1d);
    }

    private static string? ComposeAddress(string? line, string? district, string? city)
    {
        var parts = new[] { line, district, city }.Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join("، ", parts);
        return joined.Length == 0 ? null : joined;
    }




    public async Task<Result<PropertyDetailsDto>> CreateAsync(
        Guid ownerUserId, CreatePropertyRequestDto request, CancellationToken ct)
    {
        // Only an owner lists a unit. A tenant or broker account calling this is a client bug, not a
        // reason to silently create a property they can never see in their own list.
        var accountType = await _db.Users
            .Where(u => u.Id == ownerUserId && !u.IsDeleted)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.NotFound, "User not found.");

        if (accountType != AccountType.Owner)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.Forbidden,
                "Only an owner account can add a property.");

        var now = _time.GetUtcNow();

        var property = new Property
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Title = request.Title.Trim(),
            PropertyType = request.PropertyType,
            RoomsCount = request.RoomsCount,
            AnnualRent = request.AnnualRent,
            AddressLine = request.AddressLine?.Trim(),
            District = request.District?.Trim(),
            City = request.City?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CoverImageUrl = request.CoverImageUrl?.Trim(),
            Images = MapImages(request.Images),
            CreatedAt = now,
            UpdatedAt = now
        };

        // BrokerId is deliberately not settable here. A brokerage is granted management through the
        // authorization request flow, not by an owner naming one on the form.

        _db.Properties.Add(property);
        await _db.SaveChangesAsync(ct);

        return await GetDetailsAsync(property.Id, ownerUserId, ct);
    }

    public async Task<Result<PropertyDetailsDto>> UpdateAsync(
        Guid propertyId, Guid callerUserId, UpdatePropertyRequestDto request, CancellationToken ct)
    {
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == propertyId && !p.IsDeleted, ct);

        if (property is null)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.NotFound, "Property not found.");

        // Editing is the owner's alone. A managing broker can read the property but not rewrite the
        // rent or the address on it.
        if (property.OwnerUserId != callerUserId)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.Forbidden,
                "Only the owner can edit this property.");

        if (request.Title is not null) property.Title = request.Title.Trim();
        if (request.PropertyType is { } type) property.PropertyType = type;
        if (request.RoomsCount is { } rooms) property.RoomsCount = rooms;
        if (request.AnnualRent is { } rent) property.AnnualRent = rent;

        if (request.AddressLine is not null) property.AddressLine = request.AddressLine.Trim();
        if (request.District is not null) property.District = request.District.Trim();
        if (request.City is not null) property.City = request.City.Trim();

        if (request.Latitude is { } lat) property.Latitude = lat;
        if (request.Longitude is { } lng) property.Longitude = lng;

        if (request.CoverImageUrl is not null) property.CoverImageUrl = request.CoverImageUrl.Trim();

        // Whole-list replace: the gallery is one JSON column with no per-image id to patch against.
        if (request.Images is not null) property.Images = MapImages(request.Images);

        property.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        return await GetDetailsAsync(property.Id, callerUserId, ct);
    }

    public async Task<Result<bool>> DeleteAsync(Guid propertyId, Guid callerUserId, CancellationToken ct)
    {
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == propertyId && !p.IsDeleted, ct);

        if (property is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Property not found.");

        if (property.OwnerUserId != callerUserId)
            return Result<bool>.Fail(AppErrorCode.Forbidden, "Only the owner can delete this property.");

        // A unit with someone living in it cannot be delisted. The lease has to be ended first, which
        // is a deliberate act with its own endpoint and its own record.
        var hasActiveContract = await _db.Contracts
            .AnyAsync(c => c.PropertyId == propertyId && c.Status == ContractStatus.Active, ct);

        if (hasActiveContract)
            return Result<bool>.Fail(AppErrorCode.Conflict,
                "This property has an active contract. End the contract before deleting it.");

        var now = _time.GetUtcNow();
        property.IsDeleted = true;
        property.DeletedAt = now;
        property.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Ok(true);
    }

    private static List<PropertyImage> MapImages(IReadOnlyList<PropertyImageDto>? images) =>
        images is null
            ? []
            : images.Select(i => new PropertyImage
            {
                Url = i.Url.Trim(),
                Caption = i.Caption?.Trim(),
                SortOrder = i.SortOrder
            }).ToList();
}
