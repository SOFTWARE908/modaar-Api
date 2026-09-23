using modaar.api.Common.Results;
using modaar.api.Features.Dashboards.Dtos;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Dashboards.Services;

public interface IDashboardService
{
    Task<Result<OwnerDashboardDto>> GetOwnerAsync(Guid userId, CancellationToken ct);
    Task<Result<TenantDashboardDto>> GetTenantAsync(Guid userId, CancellationToken ct);
    Task<Result<BrokerDashboardDto>> GetBrokerAsync(Guid userId, CancellationToken ct);
}
