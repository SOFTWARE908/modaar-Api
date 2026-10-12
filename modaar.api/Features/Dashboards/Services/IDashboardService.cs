using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Dashboards.Dtos;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Dashboards.Services;

public interface IDashboardService
{
    Task<Result<OwnerDashboardDto>> GetOwnerAsync(Guid userId, AccountType activeRole, CancellationToken ct);
    Task<Result<TenantDashboardDto>> GetTenantAsync(Guid userId, AccountType activeRole, CancellationToken ct);
    Task<Result<BrokerDashboardDto>> GetBrokerAsync(Guid userId, AccountType activeRole, CancellationToken ct);
}
