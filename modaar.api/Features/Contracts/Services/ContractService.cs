using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Contracts.Dtos;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Persistence;
using modaar.api.Common.Localization;

namespace modaar.api.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    // How close to EndDate the renew button lights up. Mirrors PropertyService; worth pulling
    // into configuration once a second slice needs it.
    private const int RenewalWindowDays = 60;

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        ModaarDbContext db,
        TimeProvider time,
        IRequestLanguage language,
        ILogger<ContractService> logger)
    {
        _db = db;
        _time = time;
        _language = language;
        _logger = logger;
    }

    public async Task<Result<PagedResultDto<ContractDto>>> ListAsync(
        Guid callerUserId, ContractListQueryDto query, CancellationToken ct)
    {
        var accountType = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<PagedResultDto<ContractDto>>.Fail(AppErrorCode.NotFound, "User not found.");

        var contracts = Scope(_db.Contracts.AsNoTracking(), callerUserId, accountType.Value);

        if (query.Status is { } status)
            contracts = contracts.Where(c => c.Status == status);

        if (query.PropertyId is { } propertyId)
            contracts = contracts.Where(c => c.PropertyId == propertyId);

        var totalCount = await contracts.CountAsync(ct);

        var items = await contracts
            .OrderByDescending(c => c.StartDate)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .Select(c => new ContractDto
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                Title = c.Title ?? _db.Properties.Where(p => p.Id == c.PropertyId)
                    .Select(p => p.Title).FirstOrDefault() ?? string.Empty,
                MonthlyAmount = c.MonthlyAmount,
                Currency = c.Currency,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                PdfUrl = c.PdfUrl,
                PropertyId = c.PropertyId,
                PropertyTitle = _db.Properties.Where(p => p.Id == c.PropertyId)
                    .Select(p => p.Title).FirstOrDefault() ?? string.Empty
            })
            .ToListAsync(ct);

        return Result<PagedResultDto<ContractDto>>.Ok(
            new PagedResultDto<ContractDto> { Items = items, TotalCount = totalCount });
    }

    public async Task<Result<ContractDetailsDto>> GetDetailsAsync(
        Guid contractId, Guid callerUserId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(contractId, callerUserId, ct);
        if (access.Failure is not null)
            return Result<ContractDetailsDto>.Fail(access.Failure.ErrorCode!.Value, access.Failure.Error!);

        var contract = access.Contract!;
        var today = SaudiTime.Today(_time);

        var propertyTitle = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == contract.PropertyId)
            .Select(p => p.Title)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        BrokerSummaryDto? broker = null;
        if (contract.BrokerId is { } brokerId)
        {
            var row = await _db.Brokers
                .AsNoTracking()
                .Where(b => b.Id == brokerId)
                .Select(b => new
                {
                    b.Id,
                    b.SubtitleAr,
                    b.SubtitleEn,
                    b.RatingAverage,
                    b.ReviewsCount,
                    b.IsVerified,
                    Name = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.FullName).FirstOrDefault(),
                    LogoUrl = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            broker = row is null ? null : new BrokerSummaryDto
            {
                Id = row.Id,
                Name = row.Name ?? string.Empty,
                LogoUrl = row.LogoUrl,
                Subtitle = _language.Pick(row.SubtitleAr, row.SubtitleEn),
                RatingAverage = row.RatingAverage,
                ReviewsCount = row.ReviewsCount,
                IsVerified = row.IsVerified
            };
        }

        var historyRows = await _db.ContractRequests
    .AsNoTracking()
    .Where(r => r.ContractId == contractId)
    .OrderByDescending(r => r.CreatedAt)
    .Select(r => new
    {
        r.Id,
        r.Type,
        r.Status,
        r.Notes,
        r.DecisionReason,
        r.CreatedAt,
        r.DecidedAt
    })
    .ToListAsync(ct);

        var history = historyRows.Select(r => new ContractRequestHistoryItemDto
        {
            Id = r.Id,
            Title = ContractRequestLabels.Title(r.Type, _language),
            Type = r.Type,
            Status = r.Status,
            Notes = r.Notes,
            DecisionReason = r.DecisionReason,
            Date = r.CreatedAt,
            DecidedAt = r.DecidedAt
        }).ToList();

        var availability = RenewalAvailability(contract, history, today);
        var remainingDays = contract.EndDate.DayNumber - today.DayNumber;

        return Result<ContractDetailsDto>.Ok(new ContractDetailsDto
        {
            Contract = new ContractDto
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                Title = contract.Title ?? propertyTitle,
                MonthlyAmount = contract.MonthlyAmount,
                Currency = contract.Currency,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                Status = contract.Status,
                PdfUrl = contract.PdfUrl,
                PropertyId = contract.PropertyId,
                PropertyTitle = propertyTitle
            },
            Description = contract.Description,
            ImageUrls = contract.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            Broker = broker,
            RequestsHistory = history,
            RenewalAvailability = availability,
            CanRenew = availability == ContractRenewalAvailability.Available,
            RemainingDays = remainingDays
        });
    }

    public async Task<Result<string>> GetPdfUrlAsync(Guid contractId, Guid callerUserId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(contractId, callerUserId, ct);
        if (access.Failure is not null)
            return Result<string>.Fail(access.Failure.ErrorCode!.Value, access.Failure.Error!);

        // No generator exists. PdfUrl is whatever was uploaded for this lease, and is null until
        // someone does that — so this is a 404 rather than an empty string.
        if (string.IsNullOrWhiteSpace(access.Contract!.PdfUrl))
            return Result<string>.Fail(AppErrorCode.NotFound, "No document has been uploaded for this contract.");

        return Result<string>.Ok(access.Contract.PdfUrl!);
    }

    public async Task<Result<ContractRequestHistoryItemDto>> RequestRenewalAsync(
        Guid contractId, Guid callerUserId, RenewContractRequestDto request, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(contractId, callerUserId, ct);
        if (access.Failure is not null)
            return Result<ContractRequestHistoryItemDto>.Fail(access.Failure.ErrorCode!.Value, access.Failure.Error!);

        // Renewal is the tenant's ask. An owner wanting to extend does it by issuing a new lease.
        if (!access.IsTenant)
            return Result<ContractRequestHistoryItemDto>.Fail(
                AppErrorCode.Forbidden, "Only the tenant can request a renewal.");

        var contract = access.Contract!;
        var today = SaudiTime.Today(_time);

        if (contract.Status != ContractStatus.Active)
            return Result<ContractRequestHistoryItemDto>.Fail(
                AppErrorCode.Conflict, "This contract is no longer active.");

        var remainingDays = contract.EndDate.DayNumber - today.DayNumber;
        if (remainingDays > RenewalWindowDays)
            return Result<ContractRequestHistoryItemDto>.Fail(AppErrorCode.Conflict,
                $"Renewal opens {RenewalWindowDays} days before the contract ends.");

        // The filtered unique index enforces this as well; a 409 reads better than a constraint
        // violation surfacing as a 500.
        var alreadyPending = await _db.ContractRequests.AnyAsync(r =>
            r.ContractId == contractId &&
            r.Type == ContractRequestType.Renewal &&
            r.Status == ContractRequestStatus.Pending, ct);

        if (alreadyPending)
            return Result<ContractRequestHistoryItemDto>.Fail(
                AppErrorCode.Conflict, "A renewal request is already pending.");

        var created = await AddRequestAsync(
            contractId, callerUserId, ContractRequestType.Renewal, request.Notes, ct);

        _logger.LogInformation("Renewal requested on contract {Number} by {UserId}.",
            contract.ContractNumber, callerUserId);

        return Result<ContractRequestHistoryItemDto>.Ok(created);
    }

    public async Task<Result<ContractRequestHistoryItemDto?>> EndAsync(
        Guid contractId, Guid callerUserId, EndContractRequestDto request, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(contractId, callerUserId, ct);
        if (access.Failure is not null)
            return Result<ContractRequestHistoryItemDto?>.Fail(access.Failure.ErrorCode!.Value, access.Failure.Error!);

        var contract = access.Contract!;

        if (contract.Status != ContractStatus.Active)
            return Result<ContractRequestHistoryItemDto?>.Fail(
                AppErrorCode.Conflict, "This contract is no longer active.");

        var now = _time.GetUtcNow();
        var today = SaudiTime.Today(_time);

        // A tenant cannot evict themselves out of their own obligations, so their "end" is a
        // request the owner answers. The owner ending it is the decision itself.
        if (access.IsTenant)
        {
            var alreadyPending = await _db.ContractRequests.AnyAsync(r =>
                r.ContractId == contractId &&
                r.Type == ContractRequestType.Termination &&
                r.Status == ContractRequestStatus.Pending, ct);

            if (alreadyPending)
                return Result<ContractRequestHistoryItemDto?>.Fail(
                    AppErrorCode.Conflict, "A termination request is already pending.");

            var created = await AddRequestAsync(
                contractId, callerUserId, ContractRequestType.Termination, request.Reason.Trim(), ct);

            _logger.LogInformation("Termination requested on contract {Number} by tenant {UserId}.",
                contract.ContractNumber, callerUserId);

            return Result<ContractRequestHistoryItemDto?>.Ok(created);
        }

        if (!access.IsOwner)
            return Result<ContractRequestHistoryItemDto?>.Fail(
                AppErrorCode.Forbidden, "Only the owner can end this contract.");

        contract.Status = ContractStatus.Terminated;
        contract.TerminatedOn = today;
        contract.TerminationReason = request.Reason.Trim();
        contract.UpdatedAt = now;

        // Any outstanding request on a dead lease can never be answered; close it rather than
        // leaving it pending forever.
        var openRequests = await _db.ContractRequests
            .Where(r => r.ContractId == contractId && r.Status == ContractRequestStatus.Pending)
            .ToListAsync(ct);

        foreach (var open in openRequests)
        {
            open.Status = ContractRequestStatus.Cancelled;
            open.DecidedByUserId = callerUserId;
            open.DecidedAt = now;
            open.DecisionReason = "Contract ended.";
            open.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Contract {Number} terminated by owner {UserId}; {Count} open request(s) cancelled.",
            contract.ContractNumber, callerUserId, openRequests.Count);

        // Nothing to return: the lease was ended, not requested.
        return Result<ContractRequestHistoryItemDto?>.Ok(null);
    }

    private async Task<ContractRequestHistoryItemDto> AddRequestAsync(
        Guid contractId, Guid requestedByUserId, ContractRequestType type, string? notes, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        var entity = new ContractRequest
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            Type = type,
            Status = ContractRequestStatus.Pending,
            RequestedByUserId = requestedByUserId,
            Notes = notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ContractRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ContractRequestHistoryItemDto
        {
            Id = entity.Id,
            Title = ContractRequestLabels.Title(entity.Type, _language),
            Type = entity.Type,
            Status = entity.Status,
            Notes = entity.Notes,
            DecisionReason = null,
            Date = entity.CreatedAt,
            DecidedAt = null
        };
    }

    private IQueryable<Contract> Scope(IQueryable<Contract> contracts, Guid callerUserId, AccountType accountType) =>
        accountType switch
        {
            AccountType.Tenant => contracts.Where(c => c.TenantUserId == callerUserId),

            AccountType.Owner => contracts.Where(c =>
                _db.Properties.Any(p => p.Id == c.PropertyId && p.OwnerUserId == callerUserId)),

            AccountType.Broker => contracts.Where(c =>
                _db.Brokers.Any(b => b.Id == c.BrokerId && b.UserId == callerUserId)),

            // Technicians have no business with leases.
            _ => contracts.Where(_ => false)
        };

    // Loads the lease and works out who the caller is to it. Every endpoint starts here so the
    // not-found and forbidden shapes are decided once.
    private async Task<ContractAccess> ResolveAccessAsync(Guid contractId, Guid callerUserId, CancellationToken ct)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null)
            return new ContractAccess { Failure = Result<bool>.Fail(AppErrorCode.NotFound, "Contract not found.") };

        var property = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == contract.PropertyId)
            .Select(p => new { p.OwnerUserId })
            .FirstOrDefaultAsync(ct);

        var isTenant = contract.TenantUserId == callerUserId;
        var isOwner = property is not null && property.OwnerUserId == callerUserId;
        var isBroker = contract.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        if (!isTenant && !isOwner && !isBroker)
            return new ContractAccess
            {
                Failure = Result<bool>.Fail(AppErrorCode.Forbidden, "You do not have access to this contract.")
            };

        return new ContractAccess
        {
            Contract = contract,
            IsTenant = isTenant,
            IsOwner = isOwner,
            IsBroker = isBroker
        };
    }

    private static ContractRenewalAvailability RenewalAvailability(
        Contract contract, IReadOnlyList<ContractRequestHistoryItemDto> history, DateOnly today)
    {
        if (history.Any(r => r.Type == ContractRequestType.Renewal && r.Status == ContractRequestStatus.Pending))
            return ContractRenewalAvailability.Pending;

        if (contract.Status != ContractStatus.Active)
            return ContractRenewalAvailability.Unavailable;

        var remainingDays = contract.EndDate.DayNumber - today.DayNumber;

        return remainingDays is >= 0 && remainingDays <= RenewalWindowDays
            ? ContractRenewalAvailability.Available
            : ContractRenewalAvailability.Unavailable;
    }

    private sealed class ContractAccess
    {
        public Contract? Contract { get; init; }
        public bool IsTenant { get; init; }
        public bool IsOwner { get; init; }
        public bool IsBroker { get; init; }
        public Result<bool>? Failure { get; init; }
    }
}
