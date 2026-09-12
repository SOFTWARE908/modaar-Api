using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Services;

public interface IPropertyService
{
    // Scoped to the properties the caller owns.
    Task<Result<PagedResultDto<PropertyListItemDto>>> ListForOwnerAsync(
        Guid ownerUserId, PropertyListQueryDto query, CancellationToken ct);

    // Readable by the owner or by the brokerage currently managing the property.
    Task<Result<PropertyDetailsDto>> GetDetailsAsync(Guid propertyId, Guid callerUserId, CancellationToken ct);




    Task<Result<PropertyDetailsDto>> CreateAsync(
        Guid ownerUserId, CreatePropertyRequestDto request, CancellationToken ct);

    Task<Result<PropertyDetailsDto>> UpdateAsync(
        Guid propertyId, Guid callerUserId, UpdatePropertyRequestDto request, CancellationToken ct);

    Task<Result<bool>> DeleteAsync(Guid propertyId, Guid callerUserId, CancellationToken ct);


}

public record PropertyListQueryDto : PagedRequestDto
{
    // Null means both. Derived from whether an active contract exists, not from a column.
    public PropertyRentalStatus? Status { get; init; }

    public PropertyType? PropertyType { get; init; }

    // Matches on title, district or city.
    public string? Search { get; init; }

    public PropertyListSort Sort { get; init; } = PropertyListSort.CreatedDesc;
}

public enum PropertyListSort
{
    CreatedDesc,
    TitleAsc,
    LeaseEndAsc
}
