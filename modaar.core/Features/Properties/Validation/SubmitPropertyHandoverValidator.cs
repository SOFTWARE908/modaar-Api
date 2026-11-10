using FluentValidation;
using modaar.api.Features.Properties.Dtos;

namespace modaar.api.Features.Properties.Validation;

public sealed class SubmitPropertyHandoverValidator : AbstractValidator<SubmitPropertyHandoverDto>
{
    public SubmitPropertyHandoverValidator()
    {
        // A handover record with no photographs is worthless as evidence later, which is the
        // only reason the record exists.
        RuleFor(x => x.Images)
            .NotEmpty().WithMessage("At least one image is required.")
            .Must(i => i.Count <= 30).WithMessage("At most 30 images are allowed.");

        RuleForEach(x => x.Images)
            .NotEmpty().MaximumLength(500);

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SignatureUrl).MaximumLength(500);

        RuleFor(x => x.Status!.Value)
            .IsInEnum()
            .When(x => x.Status is not null);
    }
}
