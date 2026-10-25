using FluentValidation;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Dtos
{
    // An owner's subscription, building by building: a building can hold several owners' units, so
    // each owner has their own plan in each building.
    public record OwnerSubscriptionDto
    {
        public required Guid OwnerUserId { get; init; }
        public string? OwnerName { get; init; }

        // Broker: the buildings where they run at least one of this owner's units.
        // Owner: every building they have units in.
        public required IReadOnlyList<BuildingSubscriptionDto> Buildings { get; init; }
    }

    public record BuildingSubscriptionDto
    {
        public required Guid BuildingId { get; init; }
        public required string BuildingName { get; init; }
        public string? City { get; init; }

        // The owner's units in this building.
        public required int UnitsCount { get; init; }

        public required OwnerSubscriptionPlan Plan { get; init; }

        // Managed plan: quotes at or under this total are approved without asking the owner.
        // Null = every quote goes to the owner.
        public decimal? AutoApproveLimit { get; init; }

        // True while nobody has set it: Plan is the default.
        public required bool IsDefault { get; init; }
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
