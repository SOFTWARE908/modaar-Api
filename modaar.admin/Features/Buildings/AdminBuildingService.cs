using System.Globalization;
using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Properties.Enums;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.admin.Features.Buildings;

public record CityOption(Guid Id, string Name);

public record BuildingRow(Guid Id, string Name, string? City, string? District, int UnitsAdded, int? UnitsCount, DateTimeOffset CreatedAt);

public record UnitRow(Guid Id, string? UnitNumber, string? Floor, PropertyType Type, decimal? Area,
    string? OwnerName, string OwnerPhone, decimal AnnualRent, Guid? ActiveContractId, string? ActiveContractNumber);

public record BuildingDetails(Building Building, string? CityName, IReadOnlyList<UnitRow> Units)
{
    public bool IsFull => Building.UnitsCount is { } max && Units.Count >= max;
}

public record OwnerLookup(bool Found, string? Name, bool IsOwner, bool IsActive);

// What a failed save says, and which form field it belongs to ("" for the whole form).
public record FormError(string Field, string Message);

// Adding buildings and their units from the panel. Writes the same tables the app reads, so a
// unit added here shows in its owner's app like one they added themselves.
public class AdminBuildingService
{
    public const string SaudiCode = "+966";

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public AdminBuildingService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<List<CityOption>> CitiesAsync(CancellationToken ct) =>
        _db.Cities.Where(c => c.IsActive).OrderBy(c => c.SortOrder)
            .Select(c => new CityOption(c.Id, c.NameAr)).ToListAsync(ct);

    public async Task<List<BuildingRow>> ListAsync(string? search, CancellationToken ct)
    {
        var q = _db.Buildings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => b.Name.Contains(s) || (b.DeedNumber != null && b.DeedNumber.Contains(s))
                || (b.District != null && b.District.Contains(s)) || (b.City != null && b.City.Contains(s)));
        }

        return await q.OrderByDescending(b => b.CreatedAt)
            .Select(b => new BuildingRow(b.Id, b.Name, b.City, b.District,
                _db.Properties.Count(p => p.BuildingId == b.Id && !p.IsDeleted), b.UnitsCount, b.CreatedAt))
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<BuildingDetails?> GetAsync(Guid id, CancellationToken ct)
    {
        var building = await _db.Buildings.AsNoTracking().Include(b => b.CityLookup)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (building is null) return null;

        var cityName = building.CityLookup?.NameAr ?? building.City;

        var units = await _db.Properties.AsNoTracking()
            .Where(p => p.BuildingId == id && !p.IsDeleted)
            .OrderBy(p => p.Floor).ThenBy(p => p.UnitNumber)
            .Select(p => new UnitRow(p.Id, p.UnitNumber, p.Floor, p.PropertyType, p.Area,
                p.Owner.FullName, p.Owner.CountryCode + " " + p.Owner.PhoneNumber, p.AnnualRent,
                p.Contracts.Where(c => c.Status == api.Features.Contracts.Enums.ContractStatus.Active).Select(c => (Guid?)c.Id).FirstOrDefault(),
                p.Contracts.Where(c => c.Status == api.Features.Contracts.Enums.ContractStatus.Active).Select(c => c.ContractNumber).FirstOrDefault()))
            .ToListAsync(ct);

        return new BuildingDetails(building, cityName, units);
    }

    // The phone of the owner of the building's latest unit, to prefill the next unit's form.
    public Task<string?> LastOwnerPhoneAsync(Guid buildingId, CancellationToken ct) =>
        _db.Properties.Where(p => p.BuildingId == buildingId && !p.IsDeleted && p.Owner.CountryCode == SaudiCode)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.Owner.PhoneNumber)
            .FirstOrDefaultAsync(ct);

    public async Task<OwnerLookup> LookupOwnerAsync(string phone, CancellationToken ct)
    {
        var national = NationalNumber(phone);
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.CountryCode == SaudiCode && u.PhoneNumber == national && !u.IsDeleted)
            .Select(u => new { u.FullName, u.AccountTypes, u.IsActive })
            .FirstOrDefaultAsync(ct);

        return user is null
            ? new OwnerLookup(false, null, false, false)
            : new OwnerLookup(true, user.FullName, user.AccountTypes.Contains(AccountType.Owner), user.IsActive);
    }

    public async Task<(Guid? Id, List<FormError> Errors)> CreateBuildingAsync(BuildingForm form, Guid adminUserId, CancellationToken ct)
    {
        var errors = new List<FormError>();
        if (!IsValidHijriDate(form.DeedIssueDateHijri!))
            errors.Add(new(nameof(form.DeedIssueDateHijri), "تاريخ هجري غير صحيح."));

        var city = await _db.Cities.FirstOrDefaultAsync(c => c.Id == form.CityId && c.IsActive, ct);
        if (city is null)
            errors.Add(new(nameof(form.CityId), "اختر مدينة من القائمة."));

        if (errors.Count > 0) return (null, errors);

        var now = _time.GetUtcNow();
        var building = new Building
        {
            Id = Guid.NewGuid(),
            Name = form.Name!.Trim(),
            Type = form.Type!.Value,
            UsagePurpose = form.UsagePurpose!.Trim(),
            FloorsCount = form.FloorsCount,
            UnitsCount = form.UnitsCount,
            ParkingSpaces = form.ParkingSpaces,
            BuildingNumber = form.BuildingNumber,
            Street = form.Street!.Trim(),
            AdditionalNumber = form.AdditionalNumber,
            PostalCode = form.PostalCode,
            District = form.District!.Trim(),
            CityId = city!.Id,
            City = city.NameAr,
            DeedNumber = form.DeedNumber,
            DeedIssueDateHijri = form.DeedIssueDateHijri,
            DeedType = form.DeedType,
            CreatedByUserId = adminUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        // The app shows AddressLine + District + City as one line, so it carries the rest of the
        // national address: "7328 الشملول، 3885، 13241".
        building.AddressLine = $"{building.BuildingNumber} {building.Street}، {building.AdditionalNumber}، {building.PostalCode}";

        _db.Buildings.Add(building);
        await _db.SaveChangesAsync(ct);
        return (building.Id, errors);
    }

    public async Task<(Guid? Id, List<FormError> Errors)> AddUnitAsync(Guid buildingId, UnitForm form, CancellationToken ct)
    {
        var errors = new List<FormError>();

        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null)
            return (null, [new("", "المبنى غير موجود.")]);

        var unitsAdded = await _db.Properties.CountAsync(p => p.BuildingId == buildingId && !p.IsDeleted, ct);
        if (building.UnitsCount is { } max && unitsAdded >= max)
            return (null, [new("", $"المبنى مسجل بعدد {max} وحدات، وكلها اتضافت.")]);

        var unitNumber = form.UnitNumber!.Trim();
        if (await _db.Properties.AnyAsync(p => p.BuildingId == buildingId && !p.IsDeleted && p.UnitNumber == unitNumber, ct))
            errors.Add(new(nameof(form.UnitNumber), "فيه وحدة بنفس الرقم في المبنى ده."));

        if (form.AcUnitsCount > 0 && string.IsNullOrWhiteSpace(form.AcType))
            errors.Add(new(nameof(form.AcType), "هذا الحقل مطلوب لما يكون فيه تكييف."));

        // The owner: the account with this number, or a new owner account when there is none.
        var national = NationalNumber(form.OwnerPhone!);
        var owner = await _db.Users.FirstOrDefaultAsync(
            u => u.CountryCode == SaudiCode && u.PhoneNumber == national && !u.IsDeleted, ct);
        if (owner is not null && !owner.IsActive)
            errors.Add(new(nameof(form.OwnerPhone), "حساب صاحب الرقم ده موقوف."));
        if (owner is null && string.IsNullOrWhiteSpace(form.OwnerName))
            errors.Add(new(nameof(form.OwnerName), "مفيش حساب بالرقم ده، اكتب اسم المالك علشان نعمله حساب."));

        if (errors.Count > 0) return (null, errors);

        var now = _time.GetUtcNow();
        if (owner is null)
        {
            // Same shape as an account made from the app; they sign in later with this number (OTP).
            owner = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = national,
                CountryCode = SaudiCode,
                FullName = form.OwnerName!.Trim(),
                AccountTypes = [AccountType.Owner],
                ProfileStatus = ProfileStatus.Complete,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Users.Add(owner);
        }
        else if (!owner.AccountTypes.Contains(AccountType.Owner))
        {
            // E.g. a tenant who also owns: gets the owner role next to theirs, like switching in the app.
            owner.AccountTypes = [.. owner.AccountTypes, AccountType.Owner];
            owner.UpdatedAt = now;
        }

        var unit = new Property
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.Id,
            BuildingId = building.Id,
            UnitNumber = unitNumber,
            Floor = form.Floor!.Trim(),
            Title = $"{building.Name} - {unitNumber}",
            PropertyType = form.PropertyType!.Value,
            Area = form.Area,
            HasKitchenCabinets = form.HasKitchenCabinets,
            AcUnitsCount = form.AcUnitsCount,
            AcType = string.IsNullOrWhiteSpace(form.AcType) ? null : form.AcType.Trim(),
            ElectricityMeterNumber = form.ElectricityMeterNumber!.ToUpperInvariant(),
            // Optional in the panel; the app's columns are not nullable, so empty is 0.
            RoomsCount = form.RoomsCount ?? 0,
            AnnualRent = form.AnnualRent ?? 0,
            AddressLine = building.AddressLine,
            District = building.District,
            City = building.City,
            Latitude = building.Latitude,
            Longitude = building.Longitude,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Properties.Add(unit);
        await _db.SaveChangesAsync(ct);
        return (unit.Id, errors);
    }

    // As the app stores it: no country code, no leading 0.
    public static string NationalNumber(string phone) => phone.Trim().TrimStart('0');

    // yyyy-MM-dd on the Umm al-Qura calendar (the one Saudi deeds use).
    public static bool IsValidHijriDate(string value)
    {
        var parts = value.Split('-');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var d))
            return false;

        var calendar = new UmAlQuraCalendar();
        try
        {
            if (m < 1 || m > 12 || d < 1 || d > calendar.GetDaysInMonth(y, m))
                return false;

            // A deed is not issued in the future.
            return calendar.ToDateTime(y, m, d, 0, 0, 0, 0) <= DateTime.UtcNow.Date.AddDays(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Outside the years the calendar covers (1318–1500).
            return false;
        }
    }
}
