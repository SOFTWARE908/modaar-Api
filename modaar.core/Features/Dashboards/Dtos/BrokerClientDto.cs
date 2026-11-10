namespace modaar.api.Features.Dashboards.Dtos;

// An owner whose properties this brokerage manages, with how many of their units it holds.
public record BrokerClientDto
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public required int UnitsCount { get; init; }
}
