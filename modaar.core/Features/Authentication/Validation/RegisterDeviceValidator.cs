using FluentValidation;
using modaar.api.Features.Authentication.Dtos;

namespace modaar.api.Features.Authentication.Validation;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceDto>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(200);
        // FCM tokens run around 150–200 characters today; the ceiling leaves room for change.
        RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Platform).MaximumLength(20);
    }
}
