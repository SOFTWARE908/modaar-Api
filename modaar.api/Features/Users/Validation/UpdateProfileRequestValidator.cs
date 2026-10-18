using FluentValidation;
using modaar.api.Features.Authentication.Enums;
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

        When(x => x.AccountTypes is not null, () =>
        {
            RuleFor(x => x.AccountTypes!)
                .NotEmpty().WithMessage("At least one account type is required.")
                .Must(t => t.Distinct().Count() == t.Count).WithMessage("Account types must be unique.");

            RuleForEach(x => x.AccountTypes!)
                .IsInEnum()
                // Assigned by whoever onboards the tradesperson, never chosen in the app.
                .NotEqual(AccountType.Technician).WithMessage("That account type cannot be selected.")
                // Granted only by a manual UPDATE on the database. Selectable here, any user could
                // make themselves admin and then pick it with select-role.
                .NotEqual(AccountType.Admin).WithMessage("That account type cannot be selected.");
        });

        When(x => x.Language is not null, () =>
            RuleFor(x => x.Language!)
                .Must(l => l is "ar" or "en")
                .WithMessage("Language must be 'ar' or 'en'."));
    }
}
