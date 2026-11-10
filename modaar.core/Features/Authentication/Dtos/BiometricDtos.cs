namespace modaar.api.Features.Authentication.Dtos;

// Sent once, while signed in, when the user turns on Face ID / fingerprint login.
public record RegisterBiometricDeviceDto
{
    public required string DeviceId { get; init; }

    // Base64 SubjectPublicKeyInfo (DER) of an ECDSA P-256 key generated in the phone's secure
    // hardware — what iOS SecKeyCopyExternalRepresentation wrapped as SPKI, or Android
    // PublicKey.getEncoded(), produces.
    public required string PublicKey { get; init; }

    // "ios" / "android".
    public string? Platform { get; init; }
}

public record BiometricChallengeRequestDto
{
    public required string DeviceId { get; init; }
}

public record BiometricChallengeResponseDto
{
    // Sign the UTF-8 bytes of this exact string with the device's private key, SHA-256.
    public required string Challenge { get; init; }
    public required int ExpiresInSeconds { get; init; }
}

public record BiometricLoginRequestDto
{
    public required string DeviceId { get; init; }

    // Base64 ECDSA signature over the challenge. DER (what iOS and Android produce by default)
    // and raw r||s are both accepted.
    public required string Signature { get; init; }
}
