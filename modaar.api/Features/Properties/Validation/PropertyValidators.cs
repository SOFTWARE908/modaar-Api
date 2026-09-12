using FluentValidation;
using modaar.api.Features.Properties.Dtos;

namespace modaar.api.Features.Properties.Validation;

public sealed class CreatePropertyRequestValidator : AbstractValidator<CreatePropertyRequestDto>
{
    public CreatePropertyRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PropertyType).IsInEnum();

        RuleFor(x => x.RoomsCount).InclusiveBetween(0, 100);
        RuleFor(x => x.AnnualRent).GreaterThan(0).LessThanOrEqualTo(100_000_000);

        RuleFor(x => x.AddressLine).MaximumLength(500);
        RuleFor(x => x.District).MaximumLength(150);
        RuleFor(x => x.City).MaximumLength(150);

        RuleFor(x => x.CoverImageUrl).MaximumLength(500);

        // Coordinates are all-or-nothing: one without the other is unusable on a map.
        RuleFor(x => x.Longitude)
            .NotNull().When(x => x.Latitude is not null)
            .WithMessage("Longitude is required when latitude is supplied.");
        RuleFor(x => x.Latitude)
            .NotNull().When(x => x.Longitude is not null)
            .WithMessage("Latitude is required when longitude is supplied.");

        RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90m, 90m).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180m, 180m).When(x => x.Longitude is not null);

        RuleFor(x => x.Images!).Must(i => i.Count <= 20)
            .When(x => x.Images is not null)
            .WithMessage("A property may have at most 20 images.");

        RuleForEach(x => x.Images).ChildRules(i =>
        {
            i.RuleFor(p => p.Url).NotEmpty().MaximumLength(500);
            i.RuleFor(p => p.Caption).MaximumLength(300);
        });
    }
}

public sealed class UpdatePropertyRequestValidator : AbstractValidator<UpdatePropertyRequestDto>
{
    public UpdatePropertyRequestValidator()
    {
        // Same shape as UpdateProfileRequestValidator: every field is optional, but a field that
        // is present has to be usable. An empty string is a mistake, not a request to blank it.
        When(x => x.Title is not null, () =>
            RuleFor(x => x.Title!).NotEmpty().MaximumLength(200));

        When(x => x.PropertyType is not null, () =>
            RuleFor(x => x.PropertyType!.Value).IsInEnum());

        When(x => x.RoomsCount is not null, () =>
            RuleFor(x => x.RoomsCount!.Value).InclusiveBetween(0, 100));

        When(x => x.AnnualRent is not null, () =>
            RuleFor(x => x.AnnualRent!.Value).GreaterThan(0).LessThanOrEqualTo(100_000_000));

        RuleFor(x => x.AddressLine).MaximumLength(500);
        RuleFor(x => x.District).MaximumLength(150);
        RuleFor(x => x.City).MaximumLength(150);
        RuleFor(x => x.CoverImageUrl).MaximumLength(500);

        RuleFor(x => x.Longitude)
            .NotNull().When(x => x.Latitude is not null)
            .WithMessage("Longitude is required when latitude is supplied.");
        RuleFor(x => x.Latitude)
            .NotNull().When(x => x.Longitude is not null)
            .WithMessage("Latitude is required when longitude is supplied.");

        RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90m, 90m).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180m, 180m).When(x => x.Longitude is not null);

        RuleFor(x => x.Images!).Must(i => i.Count <= 20)
            .When(x => x.Images is not null)
            .WithMessage("A property may have at most 20 images.");

        RuleForEach(x => x.Images).ChildRules(i =>
        {
            i.RuleFor(p => p.Url).NotEmpty().MaximumLength(500);
            i.RuleFor(p => p.Caption).MaximumLength(300);
        });
    }
}
