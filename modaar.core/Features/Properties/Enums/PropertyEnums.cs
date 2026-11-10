using System.Text.Json.Serialization;

namespace modaar.api.Features.Properties.Enums;

// The spec leaves propertyType as a free string, so this list is a guess at the Saudi rental
// market. Confirm it with product before the first migration ships — widening an enum stored
// as a string is cheap, renaming a member already in the database is not.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    Apartment,
    Villa,
    Floor,
    Studio,
    Duplex,
    Office,
    Shop,
    Warehouse,
    Land
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyHandoverStatus
{
    Draft,
    Completed
}

// Derived from whether an active contract exists, never stored. Kept here because the DTOs
// need the shape.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyRentalStatus
{
    Rented,
    NotRented
}
