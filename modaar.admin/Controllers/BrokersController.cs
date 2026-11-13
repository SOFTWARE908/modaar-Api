using Microsoft.AspNetCore.Mvc;
using modaar.admin.Features.Brokers;

namespace modaar.admin.Controllers;

// Brokerages: the firm's name and commercial registration, and the employees picked on contracts.
[Route("brokers")]
public class BrokersController : Controller
{
    private readonly AdminBrokerService _brokers;

    public BrokersController(AdminBrokerService brokers) => _brokers = brokers;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _brokers.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var (details, form) = await _brokers.GetAsync(id, ct);
        if (details is null) return NotFound();
        ViewData["Broker"] = details;
        return View(form);
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid id, BrokerCompanyForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var error = await _brokers.SaveCompanyAsync(id, form, ct);
            if (error is null)
            {
                TempData["Flash"] = "تم حفظ بيانات المنشأة.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, error);
        }

        var (details, _) = await _brokers.GetAsync(id, ct);
        if (details is null) return NotFound();
        ViewData["Broker"] = details;
        return View(form);
    }

    [HttpPost("{id:guid}/employees")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEmployee(Guid id, string? name, CancellationToken ct)
    {
        var error = await _brokers.AddEmployeeAsync(id, name, ct);
        TempData["Flash"] = error ?? "تم إضافة الموظف.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/employees/{employeeId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveEmployee(Guid id, Guid employeeId, CancellationToken ct)
    {
        await _brokers.RemoveEmployeeAsync(id, employeeId, ct);
        TempData["Flash"] = "تم حذف الموظف من القائمة.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
