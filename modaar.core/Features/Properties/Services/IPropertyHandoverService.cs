using modaar.api.Common.Results;
using modaar.api.Features.Properties.Dtos;

namespace modaar.api.Features.Properties.Services;

public interface IPropertyHandoverService
{
    // The handover for the property's current tenancy. Readable by the owner, the managing
    // broker, and the tenant it belongs to.
    Task<Result<PropertyHandoverDto>> GetAsync(Guid propertyId, Guid callerUserId, CancellationToken ct);

    // Creates the record, or edits it while it is still a draft. Owner or managing broker only.
    Task<Result<PropertyHandoverDto>> SubmitAsync(
        Guid propertyId, Guid callerUserId, SubmitPropertyHandoverDto request, CancellationToken ct);
}
