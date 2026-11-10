using Microsoft.EntityFrameworkCore;
using modaar.admin.Auth;
using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Entities;
using modaar.api.Persistence;

namespace modaar.admin.Tools;

// Makes an admin who can sign in to the panel, then exits:
//
//   dotnet modaar.admin.dll create-admin --email admin@example.com --phone 500000000 [--name "..."]
//
// An account already holding that email or phone gets the Admin role and the password; otherwise a
// new account is made. The password is never a command-line argument (it would land in the shell
// history): it is typed at a hidden prompt, or read from MODAAR_ADMIN_PASSWORD when there is no
// console (e.g. a script).
public static class CreateAdminCommand
{
    public const string Name = "create-admin";
    private const int MinPasswordLength = 10;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var options = ParseOptions(args);
        if (!options.TryGetValue("email", out var email) || string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Usage: create-admin --email <email> [--phone <mobile without country code>] [--country +966] [--name <full name>]");
            return 2;
        }
        email = AdminSignIn.NormalizeEmail(email);
        var phone = options.GetValueOrDefault("phone")?.Trim().TrimStart('0');
        var country = options.GetValueOrDefault("country") ?? "+966";
        if (!country.StartsWith('+')) country = "+" + country;

        var password = ReadPassword();
        if (password is null) return 2;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ModaarDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTimeOffset.UtcNow;

        var byEmail = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        var byPhone = phone is null ? null
            : await db.Users.FirstOrDefaultAsync(u => u.CountryCode == country && u.PhoneNumber == phone && !u.IsDeleted);

        if (byEmail is not null && byPhone is not null && byEmail.Id != byPhone.Id)
        {
            Console.Error.WriteLine("That email and that phone belong to two different accounts. Use one of them.");
            return 1;
        }

        var user = byEmail ?? byPhone;
        if (user is null)
        {
            if (phone is null)
            {
                Console.Error.WriteLine("No account has that email. Pass --phone to create a new one.");
                return 1;
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = phone,
                CountryCode = country,
                CreatedAt = now
            };
            db.Users.Add(user);
        }

        user.Email = email;
        user.PasswordHash = hasher.Hash(password);
        if (options.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name)) user.FullName = name.Trim();
        if (!user.AccountTypes.Contains(AccountType.Admin)) user.AccountTypes = [.. user.AccountTypes, AccountType.Admin];
        user.IsActive = true;
        user.UpdatedAt = now;

        await db.SaveChangesAsync();

        Console.WriteLine($"Admin ready: {email} (account {user.Id}, roles: {string.Join(", ", user.AccountTypes)}).");
        return 0;
    }

    private static string? ReadPassword()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MODAAR_ADMIN_PASSWORD");
        if (Console.IsInputRedirected)
        {
            if (string.IsNullOrEmpty(fromEnv))
            {
                Console.Error.WriteLine("No console to type the password in: set MODAAR_ADMIN_PASSWORD.");
                return null;
            }
            return Check(fromEnv);
        }

        var first = Prompt("Password: ");
        var second = Prompt("Repeat password: ");
        if (first != second)
        {
            Console.Error.WriteLine("The two passwords differ.");
            return null;
        }
        return Check(first);
    }

    private static string? Check(string password)
    {
        if (password.Length >= MinPasswordLength) return password;
        Console.Error.WriteLine($"The password must be at least {MinPasswordLength} characters.");
        return null;
    }

    // Reads without echoing what is typed.
    private static string Prompt(string label)
    {
        Console.Write(label);
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); continue; }
            if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string([.. chars]);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].StartsWith("--")) result[args[i][2..]] = args[++i];
        return result;
    }
}
