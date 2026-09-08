namespace modaar.api.Common.Auth;

public enum AuthErrorCode
{
    InvalidCredentials,
    UserNotFound,
    AccountInactive,
    AccountTypeNotAllowed,
    ProfileFieldTaken,
    OtpExpired,
    OtpInvalid,
    OtpMaxAttempts,
    OtpResendCooldown,
    InvalidRefreshToken,
    UnsupportedLoginMethod
}
