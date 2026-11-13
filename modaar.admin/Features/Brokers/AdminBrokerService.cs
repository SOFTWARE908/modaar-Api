using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Users.Entities;
using modaar.api.Persistence;

namespace modaar.admin.Features.Brokers;

public record BrokerRow(Guid Id, string? Name, string Phone, string? CompanyName, string? Cr, int Employees);

public record BrokerDetails(Guid Id, string? Name, string Phone, List<BrokerEmployee> Employees);

// The brokerage firm's details on Ejar contracts (منشأة الوساطة).
public class BrokerCompanyForm
{
    [Display(Name = "اسم منشأة الوساطة")]
    [Required(ErrorMessage = "هذا الحقل مطلوب."), StringLength(200, ErrorMessage = "200 حرف كحد أقصى.")]
    public string? CompanyName { get; set; }

    [Display(Name = "رقم السجل التجاري")]
    [Required(ErrorMessage = "هذا الحقل مطلوب."), RegularExpression(@"^\d{10}$", ErrorMessage = "10 أرقام.")]
    public string? CommercialRegistrationNumber { get; set; }
}

// Brokerages are accounts made in the app; the panel adds the firm's details and its employees,
// who are then picked on contracts.
public class AdminBrokerService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public AdminBrokerService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<List<BrokerRow>> ListAsync(CancellationToken ct) =>
        _db.Brokers.AsNoTracking()
            .Where(b => !b.User.IsDeleted)
            .OrderBy(b => b.CompanyName ?? b.User.FullName)
            .Select(b => new BrokerRow(b.Id, b.User.FullName, b.User.CountryCode + " " + b.User.PhoneNumber,
                b.CompanyName, b.CommercialRegistrationNumber,
                _db.BrokerEmployees.Count(e => e.BrokerId == b.Id && e.IsActive)))
            .ToListAsync(ct);

    public async Task<(BrokerDetails? Details, BrokerCompanyForm Form)> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.Brokers.AsNoTracking().Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id && !x.User.IsDeleted, ct);
        if (b is null) return (null, new());

        var employees = await _db.BrokerEmployees.AsNoTracking()
            .Where(e => e.BrokerId == id && e.IsActive).OrderBy(e => e.FullName).ToListAsync(ct);
        return (new BrokerDetails(b.Id, b.User.FullName, $"{b.User.CountryCode} {b.User.PhoneNumber}", employees),
            new BrokerCompanyForm { CompanyName = b.CompanyName, CommercialRegistrationNumber = b.CommercialRegistrationNumber });
    }

    public async Task<string?> SaveCompanyAsync(Guid id, BrokerCompanyForm form, CancellationToken ct)
    {
        var b = await _db.Brokers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return "الوسيط غير موجود.";
        if (await _db.Brokers.AnyAsync(x => x.Id != id && x.CommercialRegistrationNumber == form.CommercialRegistrationNumber, ct))
            return "رقم السجل التجاري ده مسجل لوسيط تاني.";

        b.CompanyName = form.CompanyName!.Trim();
        b.CommercialRegistrationNumber = form.CommercialRegistrationNumber;
        b.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> AddEmployeeAsync(Guid brokerId, string? name, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150) return "اكتب اسم الموظف (150 حرف كحد أقصى).";
        if (!await _db.Brokers.AnyAsync(b => b.Id == brokerId, ct)) return "الوسيط غير موجود.";
        if (await _db.BrokerEmployees.AnyAsync(e => e.BrokerId == brokerId && e.IsActive && e.FullName == name, ct))
            return "الموظف ده موجود بالفعل.";

        _db.BrokerEmployees.Add(new BrokerEmployee
        {
            Id = Guid.NewGuid(), BrokerId = brokerId, FullName = name, IsActive = true, CreatedAt = _time.GetUtcNow()
        });
        await _db.SaveChangesAsync(ct);
        return null;
    }

    // Deactivates rather than deletes: contracts they handled keep their name.
    public async Task RemoveEmployeeAsync(Guid brokerId, Guid employeeId, CancellationToken ct)
    {
        var e = await _db.BrokerEmployees.FirstOrDefaultAsync(x => x.Id == employeeId && x.BrokerId == brokerId, ct);
        if (e is null) return;
        e.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }
}
