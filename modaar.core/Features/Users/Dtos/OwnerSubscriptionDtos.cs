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

        // True while nobody has set it: Plan is the default.
        public required bool IsDefault { get; init; }
    }

    // Broker only. The auto-approve limit is set by the owner on the mandate, not here.
    public record UpdateOwnerSubscriptionDto
    {
        public required OwnerSubscriptionPlan Plan { get; init; }
    }
}

namespace modaar.api.Features.Users.Validation
{
    using modaar.api.Features.Users.Dtos;

    public sealed class UpdateOwnerSubscriptionValidator : AbstractValidator<UpdateOwnerSubscriptionDto>
    {
        public UpdateOwnerSubscriptionValidator() => RuleFor(x => x.Plan).IsInEnum();
    }
}
