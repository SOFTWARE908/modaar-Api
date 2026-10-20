using System.Text.Json.Serialization;
using FluentValidation;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Buildings.Dtos
{
    // A card on "عقاراتي". Every count and amount is over the caller's own units in the building
    // (an owner's units, or the units a broker manages) — a building can hold other owners' units.
    public record BuildingListItemDto
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required BuildingType Type { get; init; }
        public string? District { get; init; }
        public string? City { get; init; }
        public string? CoverImageUrl { get; init; }

        public required int UnitsCount { get; init; }
        public required int RentedUnitsCount { get; init; }
        public required int VacantUnitsCount { get; init; }
        public required int OccupancyPercent { get; init; }

        // Active leases' rent, per month (a quarterly installment counts a third, and so on).
        public required decimal MonthlyIncome { get; init; }

        // Units with rent past due and unpaid.
        public required int OverdueCount { get; init; }
    }

    public record BuildingDetailsDto
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required BuildingType Type { get; init; }
        public string? AddressLine { get; init; }
        public string? District { get; init; }
        public string? City { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? CoverImageUrl { get; init; }

        // Only the one who added the building can rename it or change its address.
        public required bool CanEdit { get; init; }

        public required decimal ExpectedMonthlyIncome { get; init; }
        public required int OccupancyPercent { get; init; }
        public required int RentedCount { get; init; }
        public required int VacantCount { get; init; }
        public required int OverdueCount { get; init; }

        public required IReadOnlyList<BuildingUnitDto> Units { get; init; }
    }

    public record BuildingUnitDto
    {
        public required Guid Id { get; init; }
        public string? UnitNumber { get; init; }
        public string? Floor { get; init; }
        public required PropertyType PropertyType { get; init; }
        public string? TenantName { get; init; }

        // vacant · overdue · paid (rented and nothing overdue)
        public required BuildingUnitPaymentStatus PaymentStatus { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BuildingUnitPaymentStatus
    {
        Paid,
        Overdue,
        Vacant
    }

    public record CreateBuildingDto
    {
        public required string Name { get; init; }
        public required BuildingType Type { get; init; }
        public string? AddressLine { get; init; }
        public string? District { get; init; }
        public string? City { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? CoverImageUrl { get; init; }
    }

    // Partial: omitted fields stay as they are.
    public record UpdateBuildingDto
    {
        public string? Name { get; init; }
        public BuildingType? Type { get; init; }
        public string? AddressLine { get; init; }
        public string? District { get; init; }
        public string? City { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? CoverImageUrl { get; init; }
    }

    // A unit added inside a building. Address comes from the building.
    public record AddBuildingUnitDto
    {
        // Broker: which of their client owners the unit belongs to (required).
        // Owner: omit — the unit is theirs.
        public Guid? OwnerUserId { get; init; }

        public string? UnitNumber { get; init; }
        public string? Floor { get; init; }
        public required PropertyType PropertyType { get; init; }
        public required int RoomsCount { get; init; }
        public decimal? Area { get; init; }
        public int? BathroomsCount { get; init; }
        public required decimal AnnualRent { get; init; }
    }
}

namespace modaar.api.Features.Buildings.Validation
{
    using modaar.api.Features.Buildings.Dtos;

    public sealed class CreateBuildingValidator : AbstractValidator<CreateBuildingDto>
    {
        public CreateBuildingValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.AddressLine).MaximumLength(500);
            RuleFor(x => x.District).MaximumLength(100);
            RuleFor(x => x.City).MaximumLength(100);
            RuleFor(x => x.CoverImageUrl).MaximumLength(500);
        }
    }

    public sealed class UpdateBuildingValidator : AbstractValidator<UpdateBuildingDto>
    {
        public UpdateBuildingValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
            RuleFor(x => x.Type!.Value).IsInEnum().When(x => x.Type is not null);
            RuleFor(x => x.AddressLine).MaximumLength(500);
            RuleFor(x => x.District).MaximumLength(100);
            RuleFor(x => x.City).MaximumLength(100);
            RuleFor(x => x.CoverImageUrl).MaximumLength(500);
        }
    }

    public sealed class AddBuildingUnitValidator : AbstractValidator<AddBuildingUnitDto>
    {
        public AddBuildingUnitValidator()
        {
            RuleFor(x => x.UnitNumber).MaximumLength(20);
            RuleFor(x => x.Floor).MaximumLength(20);
            RuleFor(x => x.PropertyType).IsInEnum();
            RuleFor(x => x.RoomsCount).InclusiveBetween(0, 100);
            RuleFor(x => x.Area!.Value).GreaterThan(0).LessThan(100_000).When(x => x.Area is not null);
            RuleFor(x => x.BathroomsCount!.Value).InclusiveBetween(0, 50).When(x => x.BathroomsCount is not null);
            RuleFor(x => x.AnnualRent).GreaterThanOrEqualTo(0);
        }
    }
}
