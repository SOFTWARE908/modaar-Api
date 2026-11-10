using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Persistence;

namespace modaar.admin.Auth;

// Email + password sign-in for admin accounts (users holding the Admin role), kept in a cookie.
public sealed class AdminSignIn
{
    public const string Role = "Admin";

    // Ties the cookie to the password it was issued for: changing the password ends old sessions.
    private const string PasswordStampClaim = "pwd_stamp";

    // Verified when the email is unknown, so a wrong email takes as long as a wrong password and
    // the timing doesn't tell which accounts exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.EnhancedHashPassword("not-a-real-password", 12);

    private readonly ModaarDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminSignIn> _logger;

    public AdminSignIn(ModaarDbContext db, IPasswordHasher hasher, TimeProvider time, ILogger<AdminSignIn> logger)
    {
        _db = db;
        _hasher = hasher;
        _time = time;
        _logger = logger;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // One message for every failure, so the form doesn't reveal whether the email exists, the
    // password was wrong, or the account isn't an admin.
    public async Task<bool> SignInAsync(HttpContext http, string email, string password, bool remember, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized && !u.IsDeleted, ct);

        var hash = string.IsNullOrEmpty(user?.PasswordHash) ? DummyHash : user.PasswordHash;
        var passwordOk = _hasher.Verify(password, hash) && user is not null && !string.IsNullOrEmpty(user.PasswordHash);

        if (!passwordOk || !user!.IsActive || !user.AccountTypes.Contains(AccountType.Admin))
        {
            _logger.LogWarning("Admin sign-in refused for {Email}.", normalized);
            return false;
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? user.Email ?? normalized),
            new Claim(ClaimTypes.Email, normalized),
            new Claim(ClaimTypes.Role, Role),
            new Claim(PasswordStampClaim, Stamp(user.PasswordHash!))
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = remember });

        user.LastLoginAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {UserId} signed in to the panel.", user.Id);
        return true;
    }

    // Runs on every request: the account must still exist, be active, hold Admin and have the
    // password the cookie was issued for. Otherwise the cookie is dropped and the admin goes back
    // to the login page.
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue(PasswordStampClaim);
        if (!Guid.TryParse(id, out var userId) || stamp is null)
        {
            await RejectAsync(context);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ModaarDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.IsActive, u.AccountTypes, u.PasswordHash })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive || !user.AccountTypes.Contains(AccountType.Admin)
            || string.IsNullOrEmpty(user.PasswordHash) || Stamp(user.PasswordHash) != stamp)
            await RejectAsync(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static string Stamp(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];
}
