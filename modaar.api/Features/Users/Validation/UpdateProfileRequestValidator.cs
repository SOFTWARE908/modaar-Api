using FluentValidation;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Users.Validation;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestValidator()
    {
        // Every field is optional, but a field that is present has to be usable: sending an empty
        // string is a mistake, not a request to blank the value.
        When(x => x.FullName is not null, () =>
            RuleFor(x => x.FullName!)
                .NotEmpty().WithMessage("Full name cannot be empty.")
                .MaximumLength(150));

        When(x => x.Email is not null, () =>
            RuleFor(x => x.Email!)
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress()
                .MaximumLength(256));

        When(x => x.NationalId is not null, () =>
            RuleFor(x => x.NationalId!)
                .NotEmpty().WithMessage("National ID cannot be empty.")
                .MaximumLength(50));

        When(x => x.ProfileImageUrl is not null, () =>
            RuleFor(x => x.ProfileImageUrl!)
                .NotEmpty().WithMessage("Profile image URL cannot be empty.")
                .MaximumLength(500));

        RuleFor(x => x.AccountType).IsInEnum();
    }
}
