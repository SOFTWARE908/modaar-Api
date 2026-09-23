namespace modaar.api.Features.Users.Dtos;

public record BrokerManagementRequestDto
{
    // Empty or omitted means the whole portfolio.
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
}
