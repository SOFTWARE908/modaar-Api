namespace modaar.api.Features.Dashboards.Dtos;

// A tile on a home screen: units, contracts, collection rate. Value is a display string, not a
// number, so a percentage and a count can share the same shape.
public record DashboardStatDto
{
    public required string Icon { get; init; }
    public required string Label { get; init; }
    public required string Value { get; init; }
}
