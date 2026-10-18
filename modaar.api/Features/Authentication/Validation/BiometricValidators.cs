using FluentValidation;
using modaar.api.Features.Authentication.Dtos;

namespace modaar.api.Features.Authentication.Validation;

public sealed class RegisterBiometricDeviceValidator : AbstractValidator<RegisterBiometricDeviceDto>
{
    public RegisterBiometricDeviceValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(200);
        // A P-256 SPKI is 91 bytes, 124 characters in base64; the ceiling leaves room without
        // inviting arbitrary blobs. Whether it parses is checked in the service.
        RuleFor(x => x.PublicKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Platform).MaximumLength(20);
    }
}

public sealed class BiometricChallengeRequestValidator : AbstractValidator<BiometricChallengeRequestDto>
{
    public BiometricChallengeRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(200);
    }
}

public sealed class BiometricLoginRequestValidator : AbstractValidator<BiometricLoginRequestDto>
{
    public BiometricLoginRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Signature).NotEmpty().MaximumLength(200);
    }
}
