using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public sealed class BrokerService : IBrokerService
{
    // How many reviews ride along on the profile before the client pages properly.
    private const int ProfileReviewPreviewCount = 3;

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;
    private readonly ILogger<BrokerService> _logger;

    public BrokerService(
        ModaarDbContext db,
        TimeProvider time,
        IRequestLanguage language,
        ILogger<BrokerService> logger)
    {
        _db = db;
        _time = time;
        _language = language;
        _logger = logger;
    }

    public async Task<Result<BrokerDto>> GetProfileAsync(
        Guid brokerId, Guid callerUserId, CancellationToken ct)
    {
        // Public to any signed-in user — the account-type matrix gives owner, tenant and broker
        // all "broker profile view".
        var broker = await _db.Brokers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == brokerId, ct);

        if (broker is null)
            return Result<BrokerDto>.Fail(AppErrorCode.NotFound, "Broker not found.");

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == broker.UserId)
            .Select(u => new { u.FullName, u.ProfileImageUrl })
            .FirstOrDefaultAsync(ct);

        var reviews = await ReviewsQuery(brokerId)
            .Take(ProfileReviewPreviewCount)
            .ToListAsync(ct);

        // TODO: pick _Ar or _En from the request's Accept-Language once the localization helper
        // exists. English until then, as everywhere else.
        return Result<BrokerDto>.Ok(new BrokerDto
        {
            Id = broker.Id,
            Name = user?.FullName ?? string.Empty,
            LogoUrl = user?.ProfileImageUrl,
            Subtitle = _language.Pick(broker.SubtitleAr, broker.SubtitleEn),
            Rating = broker.RatingAverage,
            ReviewsCount = broker.ReviewsCount,
            IsVerified = broker.IsVerified,
            LicenseNumber = broker.LicenseNumber,
            About = _language.Pick(broker.AboutAr, broker.AboutEn),
            Services = broker.Services.OrderBy(s => s.SortOrder)
        .Select(s => _language.Pick(s.NameAr, s.NameEn)).ToList(),
            CoverageAreas = broker.CoverageAreas.OrderBy(a => a.SortOrder)
        .Select(a => _language.Pick(a.NameAr, a.NameEn)).ToList(),
            Stats = broker.Stats.OrderBy(s => s.SortOrder).Select(s => new BrokerStatDto
            {
                Title = _language.Pick(s.TitleAr, s.TitleEn),
                Value = s.Value,
                Icon = s.Icon
            }).ToList(),
            Reviews = reviews
        });
    }

    public async Task<Result<BrokerReviewsPageDto>> ListReviewsAsync(
        Guid brokerId, Guid callerUserId, PagedRequestDto query, CancellationToken ct)
    {
        if (!await _db.Brokers.AnyAsync(b => b.Id == brokerId, ct))
            return Result<BrokerReviewsPageDto>.Fail(AppErrorCode.NotFound, "Broker not found.");

        var published = _db.BrokerReviews.AsNoTracking()
            .Where(r => r.BrokerId == brokerId && r.IsPublished);

        var totalCount = await published.CountAsync(ct);

        var items = await ReviewsQuery(brokerId)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        // Drives whether the client offers a "write a review" button or an edit.
        var isReviewedByMe = await _db.BrokerReviews
            .AnyAsync(r => r.BrokerId == brokerId && r.ReviewerUserId == callerUserId, ct);

        return Result<BrokerReviewsPageDto>.Ok(new BrokerReviewsPageDto
        {
            Items = items,
            TotalCount = totalCount,
            IsReviewedByMe = isReviewedByMe
        });
    }

    public async Task<Result<bool>> RequestManagementAsync(
        Guid brokerId, Guid ownerUserId, BrokerManagementRequestDto request, CancellationToken ct)
    {
        var accountType = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId && !u.IsDeleted && u.IsActive)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "User not found.");

        // Only an owner can hand over management of a property, because only an owner has one.
        if (accountType != AccountType.Owner)
            return Result<bool>.Fail(AppErrorCode.Forbidden,
                "Only an owner account can request brokerage management.");

        if (!await _db.Brokers.AnyAsync(b => b.Id == brokerId, ct))
            return Result<bool>.Fail(AppErrorCode.NotFound, "Broker not found.");

        var propertyIds = request.PropertyIds ?? [];

        if (propertyIds.Count > 0)
        {
            // Every named property must be the caller's. Otherwise an owner could offer up
            // someone else's portfolio.
            var ownedCount = await _db.Properties.CountAsync(p =>
                propertyIds.Contains(p.Id) && p.OwnerUserId == ownerUserId && !p.IsDeleted, ct);

            if (ownedCount != propertyIds.Count)
                return Result<bool>.Fail(AppErrorCode.ValidationFailed,
                    "One or more properties are unknown or not yours.");
        }

        var alreadyPending = await _db.BrokerManagementRequests.AnyAsync(r =>
            r.OwnerUserId == ownerUserId &&
            r.BrokerId == brokerId &&
            r.Status == BrokerManagementRequestStatus.Pending, ct);

        if (alreadyPending)
            return Result<bool>.Fail(AppErrorCode.Conflict,
                "You already have a pending request with this broker.");

        var now = _time.GetUtcNow();

        _db.BrokerManagementRequests.Add(new BrokerManagementRequest
        {
            Id = Guid.NewGuid(),
            BrokerId = brokerId,
            OwnerUserId = ownerUserId,
            PropertyIds = propertyIds.ToList(),
            Status = BrokerManagementRequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Management request from owner {OwnerId} to broker {BrokerId} over {Count} propert(ies).",
            ownerUserId, brokerId, propertyIds.Count == 0 ? -1 : propertyIds.Count);

        return Result<bool>.Ok(true);
    }

    

    // Shared projection so the profile preview and the paged list can never drift apart.
    private IQueryable<BrokerReviewDto> ReviewsQuery(Guid brokerId) =>
        _db.BrokerReviews
            .AsNoTracking()
            .Where(r => r.BrokerId == brokerId && r.IsPublished)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new BrokerReviewDto
            {
                Id = r.Id,
                ReviewerName = _db.Users.Where(u => u.Id == r.ReviewerUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                ReviewerImageUrl = _db.Users.Where(u => u.Id == r.ReviewerUserId)
                    .Select(u => u.ProfileImageUrl).FirstOrDefault(),
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            });
}
