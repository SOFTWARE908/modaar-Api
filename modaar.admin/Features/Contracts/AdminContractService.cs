using Microsoft.EntityFrameworkCore;
using modaar.admin.Features.Buildings;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Lookups.Entities;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.admin.Features.Contracts;

public record Option(Guid Id, string Name);
public record EmployeeGroup(string Brokerage, List<Option> Employees);

// Everything the form's drop-downs offer.
public record ContractFormData(
    List<Option> Nationalities, List<Option> IdTypes, List<Option> ContractTypes, List<Option> ContractPlaces,
    List<Option> PaymentChannels, List<EmployeeGroup> BrokerEmployees);

// The unit the contract is for, with its owner (the lessor).
public record ContractUnit(Guid PropertyId, string Title, string? UnitNumber, Guid? BuildingId, string? BuildingName,
    Guid OwnerId, string? OwnerName, string OwnerPhone, Guid? OwnerNationalityId, Guid? OwnerIdTypeId, string? OwnerNationalId);

public record PartyLookup(bool Found, string? Name, Guid? NationalityId, Guid? IdTypeId, string? NationalId,
    bool IsActive, bool IsTenant, bool IsUnitOwner);

public record ContractRow(Guid Id, string Number, string Unit, string? Building, string? Tenant,
    DateOnly Start, DateOnly End, ContractStatus Status);

public record InstallmentRow(string Title, DateOnly? DueDate, DateOnly? PeriodStart, DateOnly? PeriodEnd, decimal Amount, PaymentStatus Status);

public record ContractDetails(Contract Contract, string Unit, Guid? BuildingId, string? Building,
    User Lessor, User Tenant, IReadOnlyDictionary<Guid, string> LookupNames,
    string? Brokerage, string? BrokerCr, string? BrokerEmployee, IReadOnlyList<InstallmentRow> Installments)
{
    public string Name(Guid? id) => id is { } x && LookupNames.TryGetValue(x, out var n) ? n : "—";
}

// One installment of the schedule a contract is split into.
public record Installment(DateOnly Start, DateOnly End, decimal Amount);

// Adding a lease from the panel: the contract row, the parties' identity details on their
// accounts, and the rent schedule as Payments rows (pending), which is what the app shows as
// upcoming and overdue installments.
public class AdminContractService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public AdminContractService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<ContractFormData> FormDataAsync(CancellationToken ct)
    {
        var lookups = await _db.Lookups.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.SortOrder)
            .Select(l => new { l.Id, l.NameAr, l.Category.Code })
            .ToListAsync(ct);
        List<Option> Of(string code) => lookups.Where(l => l.Code == code).Select(l => new Option(l.Id, l.NameAr)).ToList();

        var employees = await _db.BrokerEmployees.AsNoTracking()
            .Where(e => e.IsActive && !e.Broker.User.IsDeleted)
            .Select(e => new { e.Id, e.FullName, Brokerage = e.Broker.CompanyName ?? e.Broker.User.FullName ?? e.Broker.User.PhoneNumber })
            .ToListAsync(ct);

        return new ContractFormData(
            Of(LookupCategories.Nationality), Of(LookupCategories.IdType), Of(LookupCategories.ContractType),
            Of(LookupCategories.ContractPlace), Of(LookupCategories.PaymentChannel),
            employees.GroupBy(e => e.Brokerage).OrderBy(g => g.Key)
                .Select(g => new EmployeeGroup(g.Key, g.OrderBy(e => e.FullName).Select(e => new Option(e.Id, e.FullName)).ToList()))
                .ToList());
    }

    public Task<ContractUnit?> UnitAsync(Guid propertyId, CancellationToken ct) =>
        _db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId && !p.IsDeleted)
            .Select(p => new ContractUnit(p.Id, p.Title, p.UnitNumber, p.BuildingId, p.Building != null ? p.Building.Name : null,
                p.OwnerUserId, p.Owner.FullName, p.Owner.CountryCode + " " + p.Owner.PhoneNumber,
                p.Owner.NationalityId, p.Owner.IdTypeId, p.Owner.NationalId))
            .FirstOrDefaultAsync(ct);

    // The live lease on the unit, if any, so the form can say so instead of offering a second one.
    public Task<Guid?> ActiveContractIdAsync(Guid propertyId, CancellationToken ct) =>
        _db.Contracts.Where(c => c.PropertyId == propertyId && c.Status == ContractStatus.Active)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);

    // Prefills the form with what the unit's owner already has on their account.
    public static ContractForm NewForm(ContractUnit unit) => new()
    {
        LessorName = unit.OwnerName,
        LessorNationalityId = unit.OwnerNationalityId,
        LessorIdTypeId = unit.OwnerIdTypeId,
        LessorIdNumber = unit.OwnerNationalId
    };

    public async Task<PartyLookup> LookupPartyAsync(string phone, Guid propertyId, CancellationToken ct)
    {
        var national = AdminBuildingService.NationalNumber(phone);
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.CountryCode == AdminBuildingService.SaudiCode && u.PhoneNumber == national && !u.IsDeleted, ct);
        if (user is null) return new PartyLookup(false, null, null, null, null, false, false, false);

        var ownsUnit = await _db.Properties.AnyAsync(p => p.Id == propertyId && p.OwnerUserId == user.Id, ct);
        return new PartyLookup(true, user.FullName, user.NationalityId, user.IdTypeId, user.NationalId,
            user.IsActive, user.AccountTypes.Contains(AccountType.Tenant), ownsUnit);
    }

    public async Task<List<ContractRow>> ListAsync(string? search, CancellationToken ct)
    {
        var q = _db.Contracts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.ContractNumber.Contains(s) || (c.Tenant.FullName != null && c.Tenant.FullName.Contains(s))
                || c.Tenant.PhoneNumber.Contains(s) || c.Property.Title.Contains(s));
        }

        return await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractRow(c.Id, c.ContractNumber, c.Property.UnitNumber ?? c.Property.Title,
                c.Property.Building != null ? c.Property.Building.Name : null, c.Tenant.FullName,
                c.StartDate, c.EndDate, c.Status))
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<ContractDetails?> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.Contracts.AsNoTracking()
            .Include(x => x.Property).ThenInclude(p => p.Building)
            .Include(x => x.Property).ThenInclude(p => p.Owner)
            .Include(x => x.Tenant)
            .Include(x => x.BrokerEmployee)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var names = await _db.Lookups.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.NameAr, ct);
        var broker = c.BrokerId is { } bid
            ? await _db.Brokers.AsNoTracking().Where(b => b.Id == bid)
                .Select(b => new { Name = b.CompanyName ?? b.User.FullName, b.CommercialRegistrationNumber }).FirstOrDefaultAsync(ct)
            : null;

        var installments = await _db.Payments.AsNoTracking()
            .Where(p => p.ContractId == id && p.Type == PaymentType.Rent)
            .OrderBy(p => p.DueDate)
            .Select(p => new InstallmentRow(p.Title, p.DueDate, p.PeriodStart, p.PeriodEnd, p.Amount, p.Status))
            .ToListAsync(ct);

        return new ContractDetails(c, c.Property.UnitNumber ?? c.Property.Title, c.Property.BuildingId, c.Property.Building?.Name,
            c.Property.Owner, c.Tenant, names, broker?.Name, broker?.CommercialRegistrationNumber,
            c.BrokerEmployee?.FullName, installments);
    }

    public async Task<(Guid? Id, List<FormError> Errors)> CreateAsync(Guid propertyId, ContractForm f, CancellationToken ct)
    {
        var errors = new List<FormError>();
        void Err(string field, string message) => errors.Add(new(field, message));

        var property = await _db.Properties.Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == propertyId && !p.IsDeleted, ct);
        if (property is null) return (null, [new("", "الوحدة غير موجودة.")]);
        var owner = property.Owner;

        // ── Contract ──
        var number = NormalizeNumber(f.ContractNumber!);
        if (await _db.Contracts.AnyAsync(c => c.ContractNumber == number, ct))
            Err(nameof(f.ContractNumber), "فيه عقد بنفس الرقم.");

        var start = f.StartDate!.Value;
        var end = f.EndDate!.Value;
        if (end <= start) Err(nameof(f.EndDate), "لازم تكون بعد تاريخ البداية.");
        if (f.SignedOn > end) Err(nameof(f.SignedOn), "لازم يكون قبل نهاية العقد.");

        if (errors.All(e => e.Field != nameof(f.EndDate)))
        {
            var clash = await _db.Contracts
                .Where(c => c.PropertyId == propertyId
                    && (c.Status == ContractStatus.Active
                        || (c.Status == ContractStatus.Expired && c.StartDate <= end && c.EndDate >= start)
                        || (c.Status == ContractStatus.Terminated && c.StartDate <= end && (c.TerminatedOn ?? c.EndDate) >= start)))
                .Select(c => c.ContractNumber)
                .FirstOrDefaultAsync(ct);
            if (clash is not null) Err("", $"الوحدة عليها عقد ساري أو في نفس الفترة ({clash}).");
        }

        await CheckLookupAsync(f.ContractTypeId, LookupCategories.ContractType, nameof(f.ContractTypeId), errors, ct);
        await CheckLookupAsync(f.ContractPlaceId, LookupCategories.ContractPlace, nameof(f.ContractPlaceId), errors, ct);
        await CheckLookupAsync(f.LessorNationalityId, LookupCategories.Nationality, nameof(f.LessorNationalityId), errors, ct);
        await CheckLookupAsync(f.LessorIdTypeId, LookupCategories.IdType, nameof(f.LessorIdTypeId), errors, ct);
        await CheckLookupAsync(f.TenantNationalityId, LookupCategories.Nationality, nameof(f.TenantNationalityId), errors, ct);
        await CheckLookupAsync(f.TenantIdTypeId, LookupCategories.IdType, nameof(f.TenantIdTypeId), errors, ct);

        if (f.PaymentChannelIds.Count == 0)
            Err(nameof(f.PaymentChannelIds), "اختر قناة دفع واحدة على الأقل.");
        else
        {
            var valid = await _db.Lookups.CountAsync(l => f.PaymentChannelIds.Contains(l.Id) && l.Category.Code == LookupCategories.PaymentChannel, ct);
            if (valid != f.PaymentChannelIds.Distinct().Count()) Err(nameof(f.PaymentChannelIds), "اختيار غير صحيح.");
        }

        // ── Representatives ──
        var lessorRep = f.HasLessorRepresentative ? await RepresentativeAsync(f.LessorRep, nameof(f.LessorRep), errors, ct) : null;
        var tenantRep = f.HasTenantRepresentative ? await RepresentativeAsync(f.TenantRep, nameof(f.TenantRep), errors, ct) : null;

        // ── Brokerage ──
        BrokerEmployee? employee = null;
        if (f.BrokerEmployeeId is { } empId)
        {
            employee = await _db.BrokerEmployees.FirstOrDefaultAsync(e => e.Id == empId && e.IsActive, ct);
            if (employee is null) Err(nameof(f.BrokerEmployeeId), "اختيار غير صحيح.");
        }

        // ── Tenant: the account with this number, or a new tenant account ──
        var national = AdminBuildingService.NationalNumber(f.TenantPhone!);
        var tenant = await _db.Users.FirstOrDefaultAsync(
            u => u.CountryCode == AdminBuildingService.SaudiCode && u.PhoneNumber == national && !u.IsDeleted, ct);
        if (tenant is not null && tenant.Id == owner.Id)
            Err(nameof(f.TenantPhone), "ده رقم مالك الوحدة؛ المالك مايأجرش لنفسه.");
        else if (tenant is not null && !tenant.IsActive)
            Err(nameof(f.TenantPhone), "حساب صاحب الرقم ده موقوف.");

        // National ids are unique across accounts.
        var lessorId = f.LessorIdNumber!;
        var tenantId = f.TenantIdNumber!;
        if (lessorId == tenantId) Err(nameof(f.TenantIdNumber), "نفس رقم هوية المؤجر.");
        if (await _db.Users.AnyAsync(u => u.NationalId == lessorId && u.Id != owner.Id && !u.IsDeleted, ct))
            Err(nameof(f.LessorIdNumber), "رقم الهوية ده مسجل على حساب تاني.");
        if (await _db.Users.AnyAsync(u => u.NationalId == tenantId && (tenant == null || u.Id != tenant.Id) && !u.IsDeleted, ct))
            Err(nameof(f.TenantIdNumber), "رقم الهوية ده مسجل على حساب تاني.");

        if (errors.Count > 0) return (null, errors);

        // ── Schedule ──
        var frequency = f.PaymentFrequency!.Value;
        var periods = Periods(start, end, FrequencyLabels.Months(frequency));
        var total = TotalValue(start, end, f.AnnualRent!.Value + f.AnnualWaterFee!.Value);
        var amounts = Split(total, periods.Count);

        var now = _time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        owner.FullName = f.LessorName!.Trim();
        owner.NationalityId = f.LessorNationalityId;
        owner.IdTypeId = f.LessorIdTypeId;
        owner.NationalId = lessorId;
        owner.UpdatedAt = now;

        if (tenant is null)
        {
            // Same shape as an account made from the app; they sign in later with this number (OTP).
            tenant = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = national,
                CountryCode = AdminBuildingService.SaudiCode,
                AccountTypes = [AccountType.Tenant],
                ProfileStatus = ProfileStatus.Complete,
                IsActive = true,
                CreatedAt = now
            };
            _db.Users.Add(tenant);
        }
        else if (!tenant.AccountTypes.Contains(AccountType.Tenant))
        {
            tenant.AccountTypes = [.. tenant.AccountTypes, AccountType.Tenant];
        }
        tenant.FullName = f.TenantName!.Trim();
        tenant.NationalityId = f.TenantNationalityId;
        tenant.IdTypeId = f.TenantIdTypeId;
        tenant.NationalId = tenantId;
        tenant.UpdatedAt = now;

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = number,
            PropertyId = property.Id,
            TenantUserId = tenant.Id,
            BrokerId = employee?.BrokerId ?? property.BrokerId,
            BrokerEmployeeId = employee?.Id,
            Title = property.Title,
            ContractTypeId = f.ContractTypeId,
            ContractPlaceId = f.ContractPlaceId,
            SignedOn = f.SignedOn,
            StartDate = start,
            EndDate = end,
            Status = end >= today ? ContractStatus.Active : ContractStatus.Expired,
            PaymentFrequency = frequency,
            AnnualRent = f.AnnualRent,
            AnnualWaterFee = f.AnnualWaterFee,
            SecurityDeposit = f.SecurityDeposit,
            TotalValue = total,
            InstallmentsCount = periods.Count,
            InstallmentAmount = amounts[0],
            PaymentChannelIds = f.PaymentChannelIds.Distinct().ToList(),
            LessorRepresentative = lessorRep,
            TenantRepresentative = tenantRep,
            Currency = "SAR",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Contracts.Add(contract);

        for (var i = 0; i < periods.Count; i++)
        {
            var (pStart, pEnd) = periods[i];
            _db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                // Same wording as the rows the app already shows: "إيجار يناير 2027".
                Title = $"إيجار {MonthName(pStart.Month)} {pStart.Year}",
                Amount = amounts[i],
                Currency = "SAR",
                Date = new DateTimeOffset(pStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                DueDate = pStart,
                PeriodStart = pStart,
                PeriodEnd = pEnd,
                Type = PaymentType.Rent,
                Status = PaymentStatus.Pending,
                PayerUserId = tenant.Id,
                PropertyId = property.Id,
                ContractId = contract.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return (contract.Id, errors);
    }

    private static readonly string[] Months =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];
    private static string MonthName(int month) => Months[month - 1];

    // "10131452216/1-0" and "10131452216 / 1-0" are the same record.
    public static string NormalizeNumber(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? $"{parts[0]} / {parts[1]}" : parts[0];
    }

    // The payment cycle's periods from start to end; the last one is cut short at the end date.
    public static List<(DateOnly Start, DateOnly End)> Periods(DateOnly start, DateOnly end, int monthsPerPeriod)
    {
        var list = new List<(DateOnly, DateOnly)>();
        for (var i = 0; ; i++)
        {
            var pStart = start.AddMonths(i * monthsPerPeriod);
            if (pStart > end) break;
            var pEnd = start.AddMonths((i + 1) * monthsPerPeriod).AddDays(-1);
            list.Add((pStart, pEnd < end ? pEnd : end));
        }
        return list;
    }

    // The yearly amount over the contract's length: whole months count as twelfths of a year, and
    // a leftover part-month by its days.
    public static decimal TotalValue(DateOnly start, DateOnly end, decimal perYear)
    {
        var months = 0;
        while (start.AddMonths(months + 1).AddDays(-1) <= end) months++;
        var lastFull = start.AddMonths(months);
        var leftoverDays = end.DayNumber - lastFull.DayNumber + 1;
        var value = perYear * months / 12m + (leftoverDays > 0 ? perYear * leftoverDays / 365m : 0m);
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    // Equal installments; the last absorbs the rounding so they add up to the total exactly.
    public static decimal[] Split(decimal total, int count)
    {
        var each = Math.Round(total / count, 2, MidpointRounding.AwayFromZero);
        var amounts = Enumerable.Repeat(each, count).ToArray();
        amounts[^1] = total - each * (count - 1);
        return amounts;
    }

    private async Task CheckLookupAsync(Guid? id, string category, string field, List<FormError> errors, CancellationToken ct)
    {
        if (id is { } x && !await _db.Lookups.AnyAsync(l => l.Id == x && l.Category.Code == category, ct))
            errors.Add(new(field, "اختيار غير صحيح."));
    }

    private async Task<ContractRepresentative?> RepresentativeAsync(RepresentativeForm r, string prefix, List<FormError> errors, CancellationToken ct)
    {
        const string req = "هذا الحقل مطلوب.";
        var before = errors.Count;
        if (string.IsNullOrWhiteSpace(r.FullName)) errors.Add(new($"{prefix}.{nameof(r.FullName)}", req));
        if (r.NationalityId is null) errors.Add(new($"{prefix}.{nameof(r.NationalityId)}", req));
        if (r.IdTypeId is null) errors.Add(new($"{prefix}.{nameof(r.IdTypeId)}", req));
        if (string.IsNullOrWhiteSpace(r.IdNumber)) errors.Add(new($"{prefix}.{nameof(r.IdNumber)}", req));
        else if (!System.Text.RegularExpressions.Regex.IsMatch(r.IdNumber.Trim(), ContractForm.IdNumberPattern))
            errors.Add(new($"{prefix}.{nameof(r.IdNumber)}", "10 أرقام."));
        if (string.IsNullOrWhiteSpace(r.Phone)) errors.Add(new($"{prefix}.{nameof(r.Phone)}", req));
        else if (!System.Text.RegularExpressions.Regex.IsMatch(r.Phone.Trim(), ContractForm.PhonePattern))
            errors.Add(new($"{prefix}.{nameof(r.Phone)}", "رقم جوال سعودي يبدأ بـ 5 (9 أرقام)."));
        await CheckLookupAsync(r.NationalityId, LookupCategories.Nationality, $"{prefix}.{nameof(r.NationalityId)}", errors, ct);
        await CheckLookupAsync(r.IdTypeId, LookupCategories.IdType, $"{prefix}.{nameof(r.IdTypeId)}", errors, ct);
        if (errors.Count > before) return null;

        return new ContractRepresentative
        {
            FullName = r.FullName!.Trim(),
            NationalityId = r.NationalityId,
            IdTypeId = r.IdTypeId,
            IdNumber = r.IdNumber!.Trim(),
            PhoneNumber = AdminBuildingService.NationalNumber(r.Phone!)
        };
    }
}
