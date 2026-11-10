namespace modaar.api.Features.Users.Services
{
    using modaar.api.Common.Results;
    using modaar.api.Features.Users.Dtos;

    public interface ISecuritySettingsService
    {
        Task<Result<SecuritySettingsDto>> GetAsync(Guid userId, CancellationToken ct);

        Task<Result<SecuritySettingsDto>> UpdateAsync(
            Guid userId, UpdateSecuritySettingsDto request, CancellationToken ct);
    }
}