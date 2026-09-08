using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Authentication.Services;

public sealed class AuthService : IAuthService
{
    private readonly ModaarDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpHasher _otpHasher;
    private readonly IOtpCodeGenerator _otpGenerator;
    private readonly IOtpDeliverySender _otpSender;
    private readonly IJwtTokenService _jwt;
    private readonly OtpSettings _otpSettings;
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ModaarDbContext db,
        IPasswordHasher passwordHasher,
        IOtpHasher otpHasher,
        IOtpCodeGenerator otpGenerator,
        IOtpDeliverySender otpSender,
        IJwtTokenService jwt,
        IOptions<OtpSettings> otpSettings,
        IOptions<JwtSettings> jwtSettings,
        TimeProvider time,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _otpHasher = otpHasher;
        _otpGenerator = otpGenerator;
        _otpSender = otpSender;
        _jwt = jwt;
        _otpSettings = otpSettings.Value;
        _jwtSettings = jwtSettings.Value;
        _time = time;
        _logger = logger;
    }

    // Dormant until email/username login is switched on: nothing sets PasswordHash today.
    public async Task<AuthResult<AuthSuccessResponseDto>> LoginWithPasswordAsync(string email, string password, CancellationToken ct)
    {
        var emailNorm = NormalizeEmail(email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm && !u.IsDeleted, ct);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(password, user.PasswordHash))
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.InvalidCredentials, "Invalid email or password.");

        if (!user.IsActive)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.AccountInactive, "Account is inactive.");

        user.LastLoginAt = _time.GetUtcNow();
        return await SignInAsync(user, isNewUser: false, ct);
    }

    public Task<AuthResult<SendOtpResponseDto>> RequestPhoneOtpAsync(string phoneNumber, string countryCode, CancellationToken ct)
    {
        var code = NormalizeCountryCode(countryCode);
        return IssueOtpAsync(LoginMethod.PhoneOtp, identifier: ToInternationalNumber(code, phoneNumber),
            countryCode: code, enforceCooldown: true, wrapAsResend: false, ct);
    }

    public Task<AuthResult<SendOtpResponseDto>> RequestNationalIdOtpAsync(string nationalId, CancellationToken ct) =>
        IssueOtpAsync(LoginMethod.NationalIdOtp, identifier: nationalId.Trim(), countryCode: null, enforceCooldown: true, wrapAsResend: false, ct);

    public async Task<AuthResult<ResendOtpResponseDto>> ResendPhoneOtpAsync(string phoneNumber, CancellationToken ct)
    {
        // The caller echoes back the identifier we handed them, i.e. the full international number.
        var result = await IssueOtpAsync(LoginMethod.PhoneOtp, identifier: phoneNumber.Trim(), countryCode: null, enforceCooldown: true, wrapAsResend: true, ct);
        return Map(result, "OTP resent successfully");
    }

    public async Task<AuthResult<ResendOtpResponseDto>> ResendNationalIdOtpAsync(string nationalId, CancellationToken ct)
    {
        var result = await IssueOtpAsync(LoginMethod.NationalIdOtp, identifier: nationalId.Trim(), countryCode: null, enforceCooldown: true, wrapAsResend: true, ct);
        return Map(result, "OTP resent successfully");
    }

    // The single entry point into the system. Confirming a mobile number either signs an existing
    // user in, leaving their data untouched, or mints a fresh account for that number.
    public async Task<AuthResult<AuthSuccessResponseDto>> VerifyOtpAsync(string identifier, string otpCode, CancellationToken ct)
    {
        var identifierNorm = identifier.Trim();
        var now = _time.GetUtcNow();

        var otp = await _db.OtpCodes
            .Where(o => o.Identifier == identifierNorm && o.ConsumedAt == null && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.OtpExpired, "OTP not found or expired. Please request a new one.");

        if (otp.Attempts >= _otpSettings.MaxAttempts)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.OtpMaxAttempts, "Too many failed attempts. Please request a new OTP.");

        if (!_otpHasher.Verify(otpCode, otp.CodeHash))
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(ct);
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.OtpInvalid, "Invalid OTP code.");
        }

        // Burn the code immediately: a correct code must not stay replayable if a later step fails.
        otp.ConsumedAt = now;
        await _db.SaveChangesAsync(ct);

        var user = otp.UserId is { } knownUserId
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == knownUserId && !u.IsDeleted, ct)
            : null;

        var isNewUser = false;
        if (user is null)
        {
            // Only a mobile number can mint an account. A national ID we have never seen has no
            // number to hang the account off, and the identity of an account is its number.
            if (otp.CountryCode is null)
                return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.UserNotFound,
                    "No account found for this identifier.");

            if (SplitNationalNumber(identifierNorm, otp.CountryCode) is not { } nationalNumber)
                return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.UserNotFound,
                    "No account found for this identifier.");

            // Re-check by number: the account may have appeared between issuing and verifying.
            user = await _db.Users.FirstOrDefaultAsync(u =>
                u.CountryCode == otp.CountryCode && u.PhoneNumber == nationalNumber && !u.IsDeleted, ct);

            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    PhoneNumber = nationalNumber,
                    CountryCode = otp.CountryCode,
                    AccountType = AccountType.Tenant,
                    ProfileStatus = ProfileStatus.Incomplete,
                    IsPhoneVerified = true,
                    IsActive = true
                };
                _db.Users.Add(user);
                isNewUser = true;
                _logger.LogInformation("Created account {UserId} from a confirmed mobile number.", user.Id);
            }
        }

        if (!user.IsActive)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.AccountInactive, "Account is inactive.");

        // The identifier matched a user, so the corresponding channel is now confirmed. Nothing
        // else on an existing account is touched: signing in is not a profile edit.
        if (user.CountryCode + user.PhoneNumber == identifierNorm) user.IsPhoneVerified = true;
        else if (user.NationalId == identifierNorm) user.IsEmailVerified = true; // NationalIdOtp delivers via email

        user.LastLoginAt = now;
        user.UpdatedAt = now;

        return await SignInAsync(user, isNewUser, ct);
    }

    public async Task<AuthResult<RefreshTokenResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _jwt.HashToken(refreshToken);
        var now = _time.GetUtcNow();

        var token = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= now)
            return AuthResult<RefreshTokenResponseDto>.Fail(AuthErrorCode.InvalidRefreshToken, "Invalid or expired refresh token.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId && !u.IsDeleted, ct);
        if (user is null || !user.IsActive)
            return AuthResult<RefreshTokenResponseDto>.Fail(AuthErrorCode.AccountInactive, "Account is inactive.");

        var newRaw = _jwt.CreateRefreshToken();
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _jwt.HashToken(newRaw),
            ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpiresDays)
        };
        _db.RefreshTokens.Add(newToken);

        token.RevokedAt = now;
        token.ReplacedByTokenId = newToken.Id;

        await _db.SaveChangesAsync(ct);

        return AuthResult<RefreshTokenResponseDto>.Ok(new RefreshTokenResponseDto
        {
            AccessToken = _jwt.CreateAccessToken(user),
            RefreshToken = newRaw
        });
    }

    // ---- private helpers ----

    private async Task<AuthResult<SendOtpResponseDto>> IssueOtpAsync(
        LoginMethod method,
        string identifier,
        string? countryCode,
        bool enforceCooldown,
        bool wrapAsResend,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        var latest = await _db.OtpCodes
            .Where(o => o.Identifier == identifier)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (enforceCooldown && latest is not null &&
            (now - latest.CreatedAt).TotalSeconds < _otpSettings.ResendCooldownSeconds)
        {
            return AuthResult<SendOtpResponseDto>.Fail(AuthErrorCode.OtpResendCooldown,
                $"Please wait {_otpSettings.ResendCooldownSeconds}s before requesting another OTP.");
        }

        // A resend carries only the identifier, so inherit the country code from the request that
        // started the flow. Without it an unknown number could not be turned into an account.
        countryCode ??= latest?.CountryCode;

        var user = await FindUserForOtpAsync(method, identifier, countryCode, ct);
        var deliveryTarget = method == LoginMethod.NationalIdOtp
            ? (user?.Email ?? identifier)
            : identifier;
        var channel = method == LoginMethod.PhoneOtp ? OtpDeliveryChannel.Sms : OtpDeliveryChannel.Email;

        // Only the newest code for an identifier may be redeemed; supersede any still-live ones.
        var superseded = await _db.OtpCodes
            .Where(o => o.Identifier == identifier && o.ConsumedAt == null && o.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var stale in superseded)
            stale.ConsumedAt = now;

        var code = _otpGenerator.Generate();
        _db.OtpCodes.Add(new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user?.Id,
            Identifier = identifier,
            CountryCode = method == LoginMethod.PhoneOtp ? countryCode : null,
            CodeHash = _otpHasher.Hash(code),
            ExpiresAt = now.AddMinutes(_otpSettings.ExpiresInMinutes)
        });
        await _db.SaveChangesAsync(ct);

        await _otpSender.SendAsync(deliveryTarget, code, channel, ct);

        return AuthResult<SendOtpResponseDto>.Ok(new SendOtpResponseDto
        {
            Success = true,
            Message = wrapAsResend ? "OTP resent successfully" : "OTP sent successfully",
            Identifier = identifier,
            OtpExpiresInSeconds = _otpSettings.ExpiresInMinutes * 60
        });
    }

    private async Task<User?> FindUserForOtpAsync(LoginMethod method, string identifier, string? countryCode, CancellationToken ct)
    {
        if (method == LoginMethod.NationalIdOtp)
        {
            return await _db.Users.FirstOrDefaultAsync(u =>
                u.NationalId == identifier && !u.IsDeleted, ct);
        }

        // A phone OTP always knows its country code by this point (supplied on login, inherited on
        // resend), so the number matches exactly. A miss simply means this is a new user.
        if (countryCode is not null && SplitNationalNumber(identifier, countryCode) is { } nationalNumber)
        {
            return await _db.Users.FirstOrDefaultAsync(u =>
                u.PhoneNumber == nationalNumber && u.CountryCode == countryCode && !u.IsDeleted, ct);
        }

        return null;
    }

    private async Task<AuthResult<AuthSuccessResponseDto>> SignInAsync(User user, bool isNewUser, CancellationToken ct)
    {
        var access = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _jwt.HashToken(refresh),
            ExpiresAt = _time.GetUtcNow().AddDays(_jwtSettings.RefreshTokenExpiresDays)
        });
        await _db.SaveChangesAsync(ct);

        return AuthResult<AuthSuccessResponseDto>.Ok(new AuthSuccessResponseDto
        {
            AccessToken = access,
            RefreshToken = refresh,
            AccountType = user.AccountType,
            IsNewUser = isNewUser,
            ProfileStatus = user.ProfileStatus
        });
    }

    private static AuthResult<ResendOtpResponseDto> Map(AuthResult<SendOtpResponseDto> source, string successMessage)
    {
        if (!source.Success)
            return AuthResult<ResendOtpResponseDto>.Fail(source.ErrorCode!.Value, source.Error!);

        return AuthResult<ResendOtpResponseDto>.Ok(new ResendOtpResponseDto
        {
            Success = true,
            Message = successMessage,
            Identifier = source.Value!.Identifier,
            OtpExpiresInSeconds = source.Value.OtpExpiresInSeconds
        });
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeCountryCode(string countryCode)
    {
        var trimmed = countryCode.Trim();
        return trimmed.StartsWith('+') ? trimmed : "+" + trimmed;
    }

    // OTPs are keyed on the full international number. Keying them on the national part alone let
    // +966 555000111 and +20 555000111 supersede each other's codes and share a resend cooldown.
    // The leading trunk zero is dropped so 0555000111 and 555000111 are the same account.
    private static string ToInternationalNumber(string countryCode, string phoneNumber) =>
        countryCode + phoneNumber.Trim().TrimStart('0');

    // Splits the national part back off an international number. Null when the identifier does not
    // belong to the country code, which means it cannot name an account.
    private static string? SplitNationalNumber(string internationalNumber, string countryCode) =>
        internationalNumber.StartsWith(countryCode, StringComparison.Ordinal)
            ? internationalNumber[countryCode.Length..]
            : null;
}
