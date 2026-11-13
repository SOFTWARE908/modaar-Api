using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using modaar.admin.Features.Contracts;

namespace modaar.admin.Controllers;

// Leases: list, add one for a unit (from the building page), and view one with its schedule.
[Route("contracts")]
public class ContractsController : Controller
{
    private readonly AdminContractService _contracts;

    public ContractsController(AdminContractService contracts) => _contracts = contracts;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Search"] = q;
        return View(await _contracts.ListAsync(q, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var details = await _contracts.GetAsync(id, ct);
        return details is null ? NotFound() : View(details);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid propertyId, CancellationToken ct)
    {
        var unit = await _contracts.UnitAsync(propertyId, ct);
        if (unit is null) return NotFound();
        if (await _contracts.ActiveContractIdAsync(propertyId, ct) is { } active)
        {
            TempData["Flash"] = "الوحدة عليها عقد ساري بالفعل.";
            return RedirectToAction(nameof(Details), new { id = active });
        }

        await FillViewDataAsync(unit, ct);
        return View(AdminContractService.NewForm(unit));
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid propertyId, ContractForm form, CancellationToken ct)
    {
        var unit = await _contracts.UnitAsync(propertyId, ct);
        if (unit is null) return NotFound();

        if (ModelState.IsValid)
        {
            var (id, errors) = await _contracts.CreateAsync(propertyId, form, ct);
            if (id is { } created)
            {
                TempData["Flash"] = "تم إضافة العقد وجدول الدفعات.";
                return RedirectToAction(nameof(Details), new { id = created });
            }
            foreach (var e in errors) ModelState.AddModelError(e.Field, e.Message);
        }

        await FillViewDataAsync(unit, ct);
        return View(form);
    }

    // As the tenant's phone is typed: whose number it is, and their details to prefill.
    [HttpGet("party-lookup")]
    public async Task<IActionResult> PartyLookup(string phone, Guid propertyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone) || !Regex.IsMatch(phone.Trim(), ContractForm.PhonePattern))
            return BadRequest();
        return Json(await _contracts.LookupPartyAsync(phone, propertyId, ct));
    }

    private async Task FillViewDataAsync(ContractUnit unit, CancellationToken ct)
    {
        ViewData["Unit"] = unit;
        ViewData["Options"] = await _contracts.FormDataAsync(ct);
    }
}
