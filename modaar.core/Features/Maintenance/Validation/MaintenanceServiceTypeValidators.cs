using FluentValidation;
using modaar.api.Features.Maintenance.Dtos;

namespace modaar.api.Features.Maintenance.Validation
{
    public sealed class CreateMaintenanceServiceTypeValidator
    : AbstractValidator<CreateMaintenanceServiceTypeDto>
    {
        public CreateMaintenanceServiceTypeValidator()
        {
            // camelCase, letters and digits only. Constrained because the client keys off it: a code
            // with a space or an Arabic letter in it is awkward in a Dart switch and in a URL, and
            // once something ships against it, it can never be corrected.
            RuleFor(x => x.Code)
                .NotEmpty()
                .MaximumLength(40)
                .Matches("^[a-z][a-zA-Z0-9]*$")
                .WithMessage("Code must be camelCase: start with a lowercase letter, letters and digits only.");

            RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
            RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);

            RuleFor(x => x.SortOrder!.Value)
                .GreaterThanOrEqualTo(0)
                .When(x => x.SortOrder is not null);

            RuleFor(x => x.IconUrl)
                .MaximumLength(500)
                .Must(BeAnAbsoluteHttpUrl)
                .When(x => !string.IsNullOrWhiteSpace(x.IconUrl))
                .WithMessage("IconUrl must be an absolute http or https URL.");
        }

        // Absolute and http(s) only. A relative path would resolve against whatever host the client
        // happens to be pointed at, and a non-http scheme in an <img> src is a way to get something
        // unexpected rendered on every tenant's home screen.
        internal static bool BeAnAbsoluteHttpUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public sealed class UpdateMaintenanceServiceTypeValidator
        : AbstractValidator<UpdateMaintenanceServiceTypeDto>
    {
        public UpdateMaintenanceServiceTypeValidator()
        {
            // NotEmpty where the field is present: sending "" for a name would blank the tile, which
            // is never what was meant. IconUrl is the exception — "" clears it deliberately.
            RuleFor(x => x.NameAr!).NotEmpty().MaximumLength(100).When(x => x.NameAr is not null);
            RuleFor(x => x.NameEn!).NotEmpty().MaximumLength(100).When(x => x.NameEn is not null);

            RuleFor(x => x.SortOrder!.Value)
                .GreaterThanOrEqualTo(0)
                .When(x => x.SortOrder is not null);

            RuleFor(x => x.IconUrl)
                .MaximumLength(500)
                .Must(CreateMaintenanceServiceTypeValidator.BeAnAbsoluteHttpUrl)
                .When(x => !string.IsNullOrWhiteSpace(x.IconUrl))
                .WithMessage("IconUrl must be an absolute http or https URL.");
        }
    }
}
