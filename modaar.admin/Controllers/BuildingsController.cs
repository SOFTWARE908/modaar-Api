using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using modaar.admin.Features.Buildings;

namespace modaar.admin.Controllers;

// Buildings and the units inside them: list, add a building, then add its units one by one.
[Route("buildings")]
public class BuildingsController : Controller
{
    private readonly AdminBuildingService _buildings;

    public BuildingsController(AdminBuildingService buildings) => _buildings = buildings;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Search"] = q;
        return View(await _buildings.ListAsync(q, ct));
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Cities"] = await _buildings.CitiesAsync(ct);
        return View(new BuildingForm());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BuildingForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (id, errors) = await _buildings.CreateBuildingAsync(form, AdminId(), ct);
            if (id is { } created)
            {
                TempData["Flash"] = "تم إضافة المبنى. ضيف وحداته دلوقتي.";
                return RedirectToAction(nameof(Details), new { id = created });
            }
            AddErrors(errors);
        }

        ViewData["Cities"] = await _buildings.CitiesAsync(ct);
        return View(form);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var details = await _buildings.GetAsync(id, ct);
        return details is null ? NotFound() : View(details);
    }

    [HttpGet("{id:guid}/units/create")]
    public async Task<IActionResult> CreateUnit(Guid id, CancellationToken ct)
    {
        var details = await _buildings.GetAsync(id, ct);
        if (details is null) return NotFound();
        if (details.IsFull)
        {
            TempData["Flash"] = $"المبنى مسجل بعدد {details.Building.UnitsCount} وحدات، وكلها اتضافت.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewData["Building"] = details;
        var lastPhone = await _buildings.LastOwnerPhoneAsync(id, ct);
        return View(new UnitForm { OwnerPhone = lastPhone is null ? null : "0" + lastPhone });
    }

    [HttpPost("{id:guid}/units/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUnit(Guid id, UnitForm form, string? next, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (unitId, errors) = await _buildings.AddUnitAsync(id, form, ct);
            if (unitId is not null)
            {
                TempData["Flash"] = $"تم إضافة الوحدة {form.UnitNumber}.";
                // "Save and add another" goes straight back to an empty form while units remain.
                return next == "another"
                    ? RedirectToAction(nameof(CreateUnit), new { id })
                    : RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(errors);
        }

        var details = await _buildings.GetAsync(id, ct);
        if (details is null) return NotFound();
        ViewData["Building"] = details;
        return View(form);
    }

    // Used by the unit form as the phone is typed: says whose number it is, or that a new owner
    // account will be made.
    [HttpGet("owner-lookup")]
    public async Task<IActionResult> OwnerLookup(string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone.Trim(), @"^0?5\d{8}$"))
            return BadRequest();
        return Json(await _buildings.LookupOwnerAsync(phone, ct));
    }

    private Guid AdminId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private void AddErrors(IEnumerable<FormError> errors)
    {
        foreach (var e in errors) ModelState.AddModelError(e.Field, e.Message);
    }
}
