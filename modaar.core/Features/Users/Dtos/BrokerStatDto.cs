namespace modaar.api.Features.Users.Dtos;

public record BrokerStatDto
{
    public required string Title { get; init; }

    // A display string, not a number: "98%", "120+".
    public required string Value { get; init; }

    public string? Icon { get; init; }
}
