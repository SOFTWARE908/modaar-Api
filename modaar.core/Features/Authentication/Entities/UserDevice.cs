namespace modaar.api.Features.Authentication.Entities;

// A phone a user has signed in on. Holds the biometric login key today; the FCM push token for
// notifications belongs here too when that is built.
public class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // The app's own install identifier. One biometric login per phone: registering it for a
    // second account takes it away from the first.
    public string DeviceId { get; set; } = null!;

    // "ios" / "android". Display and diagnostics only.
    public string? Platform { get; set; }

    // Base64 SubjectPublicKeyInfo of an ECDSA P-256 key. The private half never leaves the
    // phone's secure hardware and is unlocked by Face ID / fingerprint there. Null once the user
    // turns biometric login off.
    public string? BiometricPublicKey { get; set; }

    // The one outstanding login challenge: random, single use, short-lived. Cleared as soon as
    // it is checked, whether the signature was right or not.
    public string? BiometricChallenge { get; set; }
    public DateTimeOffset? BiometricChallengeExpiresAt { get; set; }

    // Firebase Cloud Messaging token: the address push notifications are sent to. Firebase
    // rotates it now and then and the app re-registers it; null once the user logs out on this
    // phone or Firebase reports it dead.
    public string? FcmToken { get; set; }
    public DateTimeOffset? FcmTokenUpdatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
