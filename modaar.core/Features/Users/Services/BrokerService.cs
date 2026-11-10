using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
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

    public async Task<Result<PagedResultDto<BrokerListItemDto>>> ListAsync(BrokersRequestDto query, CancellationToken ct)
    {
        var brokers = _db.Brokers.AsNoTracking().Where(b => !b.User.IsDeleted && b.User.IsActive);

        // The name, an area the brokerage says it covers, or where the units it runs are.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            brokers = brokers.Where(b =>
                (b.User.FullName != null && b.User.FullName.Contains(term))
                || b.CoverageAreas.Any(a => a.NameAr.Contains(term) || a.NameEn.Contains(term))
                || _db.Properties.Any(p => p.BrokerId == b.Id && !p.IsDeleted
                                           && ((p.District != null && p.District.Contains(term)) || (p.City != null && p.City.Contains(term)))));
        }

        if (!string.IsNullOrWhiteSpace(query.Service))
        {
            var code = query.Service.Trim();
            brokers = brokers.Where(b => _db.BrokerOfferedServices.Any(s => s.BrokerId == b.Id
                && _db.BrokerServiceCategories.Any(c => c.Id == s.CategoryId && c.Code == code && c.IsActive)));
        }

        var rows = brokers.Select(b => new
        {
            Broker = b,
            b.User.FullName,
            b.User.ProfileImageUrl,
            Units = _db.Properties.Count(p => p.BrokerId == b.Id && !p.IsDeleted)
        });

        rows = query.Sort == BrokerSort.Units
            ? rows.OrderByDescending(r => r.Units).ThenByDescending(r => r.Broker.RatingAverage).ThenBy(r => r.Broker.Id)
            : rows.OrderByDescending(r => r.Broker.RatingAverage).ThenByDescending(r => r.Broker.ReviewsCount).ThenBy(r => r.Broker.Id);

        var total = await rows.CountAsync(ct);
        var page = await rows.Skip(query.SkipCount).Take(query.MaxResultCount).ToListAsync(ct);
        var services = await ServicesOfAsync(page.Select(r => r.Broker.Id).ToList(), ct);

        return Result<PagedResultDto<BrokerListItemDto>>.Ok(new PagedResultDto<BrokerListItemDto>
        {
            TotalCount = total,
            Items = page.Select(r => new BrokerListItemDto
            {
                Id = r.Broker.Id,
                UserId = r.Broker.UserId,
                Name = r.FullName ?? string.Empty,
                LogoUrl = r.ProfileImageUrl,
                Subtitle = _language.Pick(r.Broker.SubtitleAr, r.Broker.SubtitleEn),
                Rating = r.Broker.RatingAverage,
                ReviewsCount = r.Broker.ReviewsCount,
                IsVerified = r.Broker.IsVerified,
                ManagedUnits = r.Units,
                YearsOfExperience = YearsSince(r.Broker.ExperienceSinceYear),
                Services = services.GetValueOrDefault(r.Broker.Id, []).Select(s => s.Name).ToList(),
                CoverageAreas = r.Broker.CoverageAreas.OrderBy(a => a.SortOrder).Select(a => _language.Pick(a.NameAr, a.NameEn)).ToList()
            }).ToList()
        });
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
            .Select(u => new { u.FullName, u.ProfileImageUrl, PhoneNumber = u.CountryCode + u.PhoneNumber })
            .FirstOrDefaultAsync(ct);

        var reviews = await Project(Published(brokerId))
            .Take(ProfileReviewPreviewCount)
            .ToListAsync(ct);

        var managedUnits = await _db.Properties.CountAsync(p => p.BrokerId == brokerId && !p.IsDeleted, ct);
        var services = (await ServicesOfAsync([brokerId], ct)).GetValueOrDefault(brokerId, []);

        var criteria = await Published(brokerId)
            .GroupBy(r => r.BrokerId)
            .Select(g => new
            {
                Responsiveness = g.Average(r => (double)r.ResponsivenessRating),
                Management = g.Average(r => (double)r.ManagementRating),
                Transparency = g.Average(r => (double)r.TransparencyRating),
                Marketing = g.Average(r => (double)r.MarketingRating)
            })
            .FirstOrDefaultAsync(ct);

        return Result<BrokerDto>.Ok(new BrokerDto
        {
            Id = broker.Id,
            UserId = broker.UserId,
            Name = user?.FullName ?? string.Empty,
            PhoneNumber = user?.PhoneNumber,
            LogoUrl = user?.ProfileImageUrl,
            Subtitle = _language.Pick(broker.SubtitleAr, broker.SubtitleEn),
            Rating = broker.RatingAverage,
            ReviewsCount = broker.ReviewsCount,
            IsVerified = broker.IsVerified,
            LicenseNumber = broker.LicenseNumber,
            About = _language.Pick(broker.AboutAr, broker.AboutEn),
            YearsOfExperience = YearsSince(broker.ExperienceSinceYear),
            ManagedUnits = managedUnits,
            Criteria = criteria is null ? null : new BrokerReviewCriteriaDto
            {
                Responsiveness = Round1(criteria.Responsiveness),
                Management = Round1(criteria.Management),
                Transparency = Round1(criteria.Transparency),
                Marketing = Round1(criteria.Marketing)
            },
            Services = services.Select(s => s.Name).ToList(),
            ServiceCodes = services.Select(s => s.Code).ToList(),
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

    public async Task<Result<BrokerDto>> GetMineAsync(Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (await MineAsync(callerUserId, role, ct) is not { } broker)
            return Result<BrokerDto>.Fail(AppErrorCode.Forbidden, "Only a broker has a brokerage profile.");

        return await GetProfileAsync(broker.Id, callerUserId, ct);
    }

    // Only what's sent changes; services and coverage areas sent replace the whole list. The
    // license number and the verified badge stay with the admin.
    public async Task<Result<BrokerDto>> UpdateMineAsync(
        Guid callerUserId, AccountType role, UpdateMyBrokerProfileDto request, CancellationToken ct)
    {
        if (await MineAsync(callerUserId, role, ct) is not { } broker)
            return Result<BrokerDto>.Fail(AppErrorCode.Forbidden, "Only a broker has a brokerage profile.");

        if (request.SubtitleAr is not null) broker.SubtitleAr = Clean(request.SubtitleAr);
        if (request.SubtitleEn is not null) broker.SubtitleEn = Clean(request.SubtitleEn);
        if (request.AboutAr is not null) broker.AboutAr = Clean(request.AboutAr);
        if (request.AboutEn is not null) broker.AboutEn = Clean(request.AboutEn);
        if (request.ExperienceSinceYear is { } year) broker.ExperienceSinceYear = year;

        if (request.CoverageAreas is { } areas)
            broker.CoverageAreas = areas.Select((a, i) => new BrokerCoverageArea
            {
                NameAr = a.NameAr.Trim(),
                NameEn = a.NameEn.Trim(),
                SortOrder = i + 1
            }).ToList();

        if (request.ServiceCodes is { } codes)
        {
            var wanted = codes.Select(c => c.Trim()).Distinct().ToList();
            var found = await _db.BrokerServiceCategories
                .Where(c => wanted.Contains(c.Code) && c.IsActive)
                .Select(c => new { c.Id, c.Code })
                .ToListAsync(ct);

            var unknown = wanted.Except(found.Select(c => c.Code)).ToList();
            if (unknown.Count > 0)
                return Result<BrokerDto>.Fail(AppErrorCode.ValidationFailed, $"Unknown or retired service(s): {string.Join(", ", unknown)}.");

            _db.BrokerOfferedServices.RemoveRange(_db.BrokerOfferedServices.Where(s => s.BrokerId == broker.Id));
            _db.BrokerOfferedServices.AddRange(found.Select(c => new BrokerOfferedService { BrokerId = broker.Id, CategoryId = c.Id }));
        }

        broker.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Broker {BrokerId} updated their profile.", broker.Id);
        return await GetProfileAsync(broker.Id, callerUserId, ct);
    }

    public async Task<Result<BrokerReviewsPageDto>> ListReviewsAsync(
        Guid brokerId, Guid callerUserId, PagedRequestDto query, CancellationToken ct)
    {
        if (!await _db.Brokers.AnyAsync(b => b.Id == brokerId, ct))
            return Result<BrokerReviewsPageDto>.Fail(AppErrorCode.NotFound, "Broker not found.");

        var totalCount = await Published(brokerId).CountAsync(ct);

        var items = await Project(Published(brokerId))
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        // Drives whether the client offers a "write a review" button or an edit, and prefills it.
        var mine = await Project(_db.BrokerReviews.Where(r => r.BrokerId == brokerId && r.ReviewerUserId == callerUserId))
            .FirstOrDefaultAsync(ct);

        return Result<BrokerReviewsPageDto>.Ok(new BrokerReviewsPageDto
        {
            Items = items,
            TotalCount = totalCount,
            IsReviewedByMe = mine is not null,
            MyReview = mine
        });
    }

    // An owner with at least one unit this broker runs now; sending again edits their review.
    public async Task<Result<BrokerReviewDto>> SubmitReviewAsync(
        Guid brokerId, Guid callerUserId, AccountType role, SubmitBrokerReviewDto request, CancellationToken ct)
    {
        if (role != AccountType.Owner)
            return Result<BrokerReviewDto>.Fail(AppErrorCode.Forbidden, "Only an owner rates a broker.");

        var broker = await _db.Brokers.FirstOrDefaultAsync(b => b.Id == brokerId, ct);
        if (broker is null)
            return Result<BrokerReviewDto>.Fail(AppErrorCode.NotFound, "Broker not found.");

        var runsMine = await _db.Properties.AnyAsync(p => p.OwnerUserId == callerUserId && p.BrokerId == brokerId && !p.IsDeleted, ct);
        if (!runsMine)
            return Result<BrokerReviewDto>.Fail(AppErrorCode.Forbidden, "You can rate a broker who runs one of your properties.");

        var now = _time.GetUtcNow();
        var review = await _db.BrokerReviews.FirstOrDefaultAsync(r => r.BrokerId == brokerId && r.ReviewerUserId == callerUserId, ct);
        if (review is null)
        {
            review = new BrokerReview { Id = Guid.NewGuid(), BrokerId = brokerId, ReviewerUserId = callerUserId, CreatedAt = now };
            _db.BrokerReviews.Add(review);
        }

        review.ResponsivenessRating = request.Responsiveness;
        review.ManagementRating = request.Management;
        review.TransparencyRating = request.Transparency;
        review.MarketingRating = request.Marketing;
        review.Rating = (int)Math.Round(
            (request.Responsiveness + request.Management + request.Transparency + request.Marketing) / 4m,
            MidpointRounding.AwayFromZero);
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        // The rollup, recalculated from scratch (see BrokerReview).
        var stats = await Published(brokerId)
            .GroupBy(r => r.BrokerId)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Rating) })
            .FirstOrDefaultAsync(ct);
        broker.ReviewsCount = stats?.Count ?? 0;
        broker.RatingAverage = Math.Round(stats?.Average ?? 0m, 2);
        broker.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Owner {OwnerId} rated broker {BrokerId} {Rating}.", callerUserId, brokerId, review.Rating);

        return Result<BrokerReviewDto>.Ok(await Project(_db.BrokerReviews.Where(r => r.Id == review.Id)).FirstAsync(ct));
    }

    // The caller's brokerage, tracked, when they're acting as a broker.
    private async Task<Broker?> MineAsync(Guid callerUserId, AccountType role, CancellationToken ct) =>
        role == AccountType.Broker
            ? await _db.Brokers.FirstOrDefaultAsync(b => b.UserId == callerUserId, ct)
            : null;

    // Each brokerage's services, in the admin's order.
    private async Task<Dictionary<Guid, List<(string Code, string Name)>>> ServicesOfAsync(
        IReadOnlyCollection<Guid> brokerIds, CancellationToken ct)
    {
        var rows = await _db.BrokerOfferedServices
            .AsNoTracking()
            .Where(s => brokerIds.Contains(s.BrokerId))
            .Join(_db.BrokerServiceCategories, s => s.CategoryId, c => c.Id,
                (s, c) => new { s.BrokerId, c.Code, c.NameAr, c.NameEn, c.SortOrder })
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.BrokerId)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Code, _language.Pick(x.NameAr, x.NameEn))).ToList());
    }

    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IQueryable<BrokerReview> Published(Guid brokerId) =>
        _db.BrokerReviews.AsNoTracking().Where(r => r.BrokerId == brokerId && r.IsPublished);

    // Shared projection so the profile preview, the paged list and "mine" can never drift apart.
    private IQueryable<BrokerReviewDto> Project(IQueryable<BrokerReview> rows) =>
        rows
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new BrokerReviewDto
            {
                Id = r.Id,
                ReviewerName = _db.Users.Where(u => u.Id == r.ReviewerUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                ReviewerImageUrl = _db.Users.Where(u => u.Id == r.ReviewerUserId)
                    .Select(u => u.ProfileImageUrl).FirstOrDefault(),
                Rating = r.Rating,
                Criteria = new BrokerReviewCriteriaDto
                {
                    Responsiveness = r.ResponsivenessRating,
                    Management = r.ManagementRating,
                    Transparency = r.TransparencyRating,
                    Marketing = r.MarketingRating
                },
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            });

    private int? YearsSince(int? year) =>
        year is { } y ? Math.Max(0, SaudiTime.Today(_time).Year - y) : null;

    private static decimal Round1(double value) => Math.Round((decimal)value, 1);
}
