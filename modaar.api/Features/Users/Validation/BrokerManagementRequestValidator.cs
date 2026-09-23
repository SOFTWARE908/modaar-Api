using FluentValidation;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Brokers.Validation;

public sealed class BrokerManagementRequestValidator : AbstractValidator<BrokerManagementRequestDto>
{
    public BrokerManagementRequestValidator()
    {
        RuleFor(x => x.PropertyIds!)
            .Must(ids => ids.Count <= 100)
                .WithMessage("At most 100 properties can be named in one request.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("Property ids must be unique.")
            .When(x => x.PropertyIds is not null);
    }
}
