using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using modaar.admin.Auth;
using modaar.api.Common.Extensions;
using modaar.api.Common.Realtime;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Business rules, DbContext and validators: the same as the API.
builder.Services.AddModaarCore(builder.Configuration);

// Notifications are for the Flutter app (decided 2026-11-10): the panel saves them like any
// service does, but pushes nothing live and runs no push worker.
builder.Services.AddSingleton<IRealtimeNotifier, NoRealtimeNotifier>();

builder.Services.AddScoped<AdminSignIn>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "modaar.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        // An admin removed, deactivated or stripped of the role is signed out on their next click.
        options.Events.OnValidatePrincipal = AdminSignIn.ValidateAsync;
    });

// Every page needs a signed-in admin unless it says [AllowAnonymous] (the login page).
var adminOnly = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireRole(AdminSignIn.Role)
    .Build();
builder.Services.AddAuthorization(o => o.FallbackPolicy = adminOnly);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AuthorizeFilter(adminOnly)));

// Write Arabic as-is in the HTML instead of &#x…; entities (still HTML-encoded for safety).
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

// Slows password guessing: 10 login attempts a minute per client address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
