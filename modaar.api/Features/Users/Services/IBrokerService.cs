using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Users.Services;

public interface IBrokerService
{
    // The marketplace: every brokerage, searched and sorted.
    Task<Result<PagedResultDto<BrokerListItemDto>>> ListAsync(BrokersRequestDto query, CancellationToken ct);

    Task<Result<BrokerDto>> GetProfileAsync(Guid brokerId, Guid callerUserId, CancellationToken ct);

    // The broker's own profile, to read and edit.
    Task<Result<BrokerDto>> GetMineAsync(Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BrokerDto>> UpdateMineAsync(Guid callerUserId, AccountType role, UpdateMyBrokerProfileDto request, CancellationToken ct);

    Task<Result<BrokerReviewsPageDto>> ListReviewsAsync(
        Guid brokerId, Guid callerUserId, PagedRequestDto query, CancellationToken ct);

    Task<Result<BrokerReviewDto>> SubmitReviewAsync(
        Guid brokerId, Guid callerUserId, AccountType role, SubmitBrokerReviewDto request, CancellationToken ct);
}
