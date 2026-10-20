using FluentValidation;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Dtos
{
    public record OwnerSubscriptionDto
    {
        public required Guid OwnerUserId { get; init; }
        public string? OwnerName { get; init; }

        public required OwnerSubscriptionPlan Plan { get; init; }

        // Managed plan: quotes at or under this total are approved without asking the owner.
        // Null = every quote goes to the owner.
        public decimal? AutoApproveLimit { get; init; }
    }

    // Broker only. Both fields are written as sent: omitting the limit clears it.
    public record UpdateOwnerSubscriptionDto
    {
        public required OwnerSubscriptionPlan Plan { get; init; }
        public decimal? AutoApproveLimit { get; init; }
    }
}

namespace modaar.api.Features.Users.Validation
{
    using modaar.api.Features.Users.Dtos;

    public sealed class UpdateOwnerSubscriptionValidator : AbstractValidator<UpdateOwnerSubscriptionDto>
    {
        public UpdateOwnerSubscriptionValidator()
        {
            RuleFor(x => x.Plan).IsInEnum();

            RuleFor(x => x.AutoApproveLimit!.Value)
                .GreaterThanOrEqualTo(0).LessThan(1_000_000)
                .When(x => x.AutoApproveLimit is not null);
        }
    }
}
