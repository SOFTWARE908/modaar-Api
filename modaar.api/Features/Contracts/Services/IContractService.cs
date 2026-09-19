using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Contracts.Dtos;

namespace modaar.api.Features.Contracts.Services;

public interface IContractService
{
    // Scoped by account type: a tenant sees their own leases, an owner sees leases on properties
    // they own, a broker sees the ones on properties they manage.
    Task<Result<PagedResultDto<ContractDto>>> ListAsync(
        Guid callerUserId, ContractListQueryDto query, CancellationToken ct);

    Task<Result<ContractDetailsDto>> GetDetailsAsync(
        Guid contractId, Guid callerUserId, CancellationToken ct);

    // Returns the stored document URL. Generation is not implemented.
    Task<Result<string>> GetPdfUrlAsync(Guid contractId, Guid callerUserId, CancellationToken ct);

    // Tenant only. Files a renewal request for the owner or broker to answer.
    Task<Result<ContractRequestHistoryItemDto>> RequestRenewalAsync(
        Guid contractId, Guid callerUserId, RenewContractRequestDto request, CancellationToken ct);

    // An owner ends the lease outright; a tenant files a termination request instead.
    Task<Result<ContractRequestHistoryItemDto?>> EndAsync(
        Guid contractId, Guid callerUserId, EndContractRequestDto request, CancellationToken ct);
}
