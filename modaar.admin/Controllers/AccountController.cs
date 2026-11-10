using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using modaar.admin.Auth;

namespace modaar.admin.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly AdminSignIn _signIn;

    public AccountController(AdminSignIn signIn) => _signIn = signIn;

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole(AdminSignIn.Role))
            return LocalRedirect(SafeReturnUrl(returnUrl));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!await _signIn.SignInAsync(HttpContext, model.Email, model.Password, model.RememberMe, ct))
        {
            ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
            return View(model with { Password = string.Empty });
        }

        return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // Only paths on this site, so a crafted ?returnUrl= can't bounce an admin to another domain.
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}

public record LoginViewModel
{
    [Required(ErrorMessage = "أدخل البريد الإلكتروني.")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "أدخل كلمة المرور.")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; init; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; init; }

    public string? ReturnUrl { get; init; }
}
