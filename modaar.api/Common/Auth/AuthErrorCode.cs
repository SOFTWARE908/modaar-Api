namespace modaar.api.Common.Auth;

public enum AuthErrorCode
{
    InvalidCredentials,
    UserNotFound,
    AccountInactive,
    AccountTypeNotAllowed,
    DuplicateRegistration,
    OtpExpired,
    OtpInvalid,
    OtpMaxAttempts,
    OtpResendCooldown,
    InvalidRefreshToken,
    UnsupportedLoginMethod
}
