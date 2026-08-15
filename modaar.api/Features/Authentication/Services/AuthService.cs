using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Entities;
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

    public async Task<AuthResult<AuthSuccessResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken ct)
    {
        if (request.AccountType == AccountType.Technician)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.AccountTypeNotAllowed,
                "Technician accounts cannot be created from the mobile app.");

        var emailNorm = NormalizeEmail(request.Email);
        var nationalIdNorm = request.NationalId.Trim();
        var phoneNorm = request.PhoneNumber.Trim();
        var countryCodeNorm = request.CountryCode.Trim();

        var clash = await _db.Users.AsNoTracking().AnyAsync(u =>
            !u.IsDeleted && (
                u.Email == emailNorm ||
                u.NationalId == nationalIdNorm ||
                (u.CountryCode == countryCodeNorm && u.PhoneNumber == phoneNorm)),
            ct);
        if (clash)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.DuplicateRegistration,
                "An account with these details already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.UserName.Trim(),
            Email = emailNorm,
            PhoneNumber = phoneNorm,
            CountryCode = countryCodeNorm,
            NationalId = nationalIdNorm,
            AccountType = request.AccountType,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var (access, refresh) = await IssueTokensAsync(user, ct);
        return AuthResult<AuthSuccessResponseDto>.Ok(new AuthSuccessResponseDto
        {
            AccessToken = access,
            RefreshToken = refresh,
            AccountType = user.AccountType
        });
    }

    public async Task<AuthResult<AuthSuccessResponseDto>> LoginWithPasswordAsync(string email, string password, CancellationToken ct)
    {
        var emailNorm = NormalizeEmail(email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm && !u.IsDeleted, ct);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(password, user.PasswordHash))
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.InvalidCredentials, "Invalid email or password.");

        if (!user.IsActive)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.AccountInactive, "Account is inactive.");

        user.LastLoginAt = _time.GetUtcNow();
        var (access, refresh) = await IssueTokensAsync(user, ct);

        return AuthResult<AuthSuccessResponseDto>.Ok(new AuthSuccessResponseDto
        {
            AccessToken = access,
            RefreshToken = refresh,
            AccountType = user.AccountType
        });
    }

    public Task<AuthResult<SendOtpResponseDto>> RequestPhoneOtpAsync(string phoneNumber, string countryCode, CancellationToken ct) =>
        IssueOtpAsync(LoginMethod.PhoneOtp, identifier: phoneNumber.Trim(), countryCode: countryCode.Trim(), enforceCooldown: false, wrapAsResend: false, ct);

    public Task<AuthResult<SendOtpResponseDto>> RequestNationalIdOtpAsync(string nationalId, CancellationToken ct) =>
        IssueOtpAsync(LoginMethod.NationalIdOtp, identifier: nationalId.Trim(), countryCode: null, enforceCooldown: false, wrapAsResend: false, ct);

    public async Task<AuthResult<ResendOtpResponseDto>> ResendPhoneOtpAsync(string phoneNumber, CancellationToken ct)
    {
        var result = await IssueOtpAsync(LoginMethod.PhoneOtp, identifier: phoneNumber.Trim(), countryCode: null, enforceCooldown: true, wrapAsResend: true, ct);
        return Map(result, "OTP resent successfully");
    }

    public async Task<AuthResult<ResendOtpResponseDto>> ResendNationalIdOtpAsync(string nationalId, CancellationToken ct)
    {
        var result = await IssueOtpAsync(LoginMethod.NationalIdOtp, identifier: nationalId.Trim(), countryCode: null, enforceCooldown: true, wrapAsResend: true, ct);
        return Map(result, "OTP resent successfully");
    }

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

        otp.ConsumedAt = now;

        if (otp.UserId is null)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.UserNotFound, "No account found for this identifier.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == otp.UserId.Value && !u.IsDeleted, ct);
        if (user is null)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.UserNotFound, "Account no longer exists.");
        if (!user.IsActive)
            return AuthResult<AuthSuccessResponseDto>.Fail(AuthErrorCode.AccountInactive, "Account is inactive.");

        // The identifier matched a user, so the corresponding channel is now confirmed.
        if (user.PhoneNumber == identifierNorm) user.IsPhoneVerified = true;
        else if (user.NationalId == identifierNorm) user.IsEmailVerified = true; // NationalIdOtp delivers via email

        user.LastLoginAt = now;

        var (access, refresh) = await IssueTokensAsync(user, ct);
        return AuthResult<AuthSuccessResponseDto>.Ok(new AuthSuccessResponseDto
        {
            AccessToken = access,
            RefreshToken = refresh,
            AccountType = user.AccountType
        });
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

    public async Task<AuthResult<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return AuthResult<UserProfileDto>.Fail(AuthErrorCode.UserNotFound, "User not found.");

        return AuthResult<UserProfileDto>.Ok(new UserProfileDto
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            CountryCode = user.CountryCode,
            NationalId = user.NationalId,
            AccountType = user.AccountType,
            ProfileImageUrl = user.ProfileImageUrl
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
        if (enforceCooldown)
        {
            var recent = await _db.OtpCodes
                .Where(o => o.Identifier == identifier)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (recent is not null &&
                (_time.GetUtcNow() - recent.CreatedAt).TotalSeconds < _otpSettings.ResendCooldownSeconds)
            {
                return AuthResult<SendOtpResponseDto>.Fail(AuthErrorCode.OtpResendCooldown,
                    $"Please wait {_otpSettings.ResendCooldownSeconds}s before requesting another OTP.");
            }
        }

        var user = await FindUserForOtpAsync(method, identifier, countryCode, ct);
        var deliveryTarget = method == LoginMethod.NationalIdOtp
            ? (user?.Email ?? identifier)
            : identifier;
        var channel = method == LoginMethod.PhoneOtp ? OtpDeliveryChannel.Sms : OtpDeliveryChannel.Email;

        var code = _otpGenerator.Generate();
        var entity = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user?.Id,
            Identifier = identifier,
            CodeHash = _otpHasher.Hash(code),
            ExpiresAt = _time.GetUtcNow().AddMinutes(_otpSettings.ExpiresInMinutes)
        };
        _db.OtpCodes.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _otpSender.SendAsync(deliveryTarget, code, channel, ct);

        return AuthResult<SendOtpResponseDto>.Ok(new SendOtpResponseDto
        {
            Success = true,
            Message = wrapAsResend ? "OTP resent successfully" : "OTP sent successfully"
        });
    }

    private async Task<User?> FindUserForOtpAsync(LoginMethod method, string identifier, string? countryCode, CancellationToken ct)
    {
        // For PhoneOtp on the initial request we have a country code → exact match.
        // On resend we only have the identifier → fall back to "remember the user from the most recent OTP".
        if (method == LoginMethod.PhoneOtp && countryCode is not null)
        {
            return await _db.Users.FirstOrDefaultAsync(u =>
                u.PhoneNumber == identifier && u.CountryCode == countryCode && !u.IsDeleted, ct);
        }

        if (method == LoginMethod.NationalIdOtp)
        {
            return await _db.Users.FirstOrDefaultAsync(u =>
                u.NationalId == identifier && !u.IsDeleted, ct);
        }

        // PhoneOtp resend without countryCode: prefer the user from the most recent OTP for this identifier.
        var lastOtp = await _db.OtpCodes
            .Where(o => o.Identifier == identifier && o.UserId != null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (lastOtp?.UserId is { } uid)
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && !u.IsDeleted, ct);

        // Last resort: phone-only lookup. May match multiple users across country codes — pick first.
        return await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier && !u.IsDeleted, ct);
    }

    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(User user, CancellationToken ct)
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

        return (access, refresh);
    }

    private static AuthResult<ResendOtpResponseDto> Map(AuthResult<SendOtpResponseDto> source, string successMessage)
    {
        if (!source.Success)
            return AuthResult<ResendOtpResponseDto>.Fail(source.ErrorCode!.Value, source.Error!);

        return AuthResult<ResendOtpResponseDto>.Ok(new ResendOtpResponseDto
        {
            Success = true,
            Message = successMessage
        });
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
