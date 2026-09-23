using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Users.Services;

public interface IBrokerService
{
    Task<Result<BrokerDto>> GetProfileAsync(Guid brokerId, Guid callerUserId, CancellationToken ct);

    Task<Result<BrokerReviewsPageDto>> ListReviewsAsync(
        Guid brokerId, Guid callerUserId, PagedRequestDto query, CancellationToken ct);

    Task<Result<bool>> RequestManagementAsync(
        Guid brokerId, Guid ownerUserId, BrokerManagementRequestDto request, CancellationToken ct);
}
