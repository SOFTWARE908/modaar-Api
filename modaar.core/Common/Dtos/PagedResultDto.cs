namespace modaar.api.Common.Dtos;

// Matches what the client already expects: a page of rows plus the total, for infinite scroll.
public record PagedResultDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }

    public static PagedResultDto<T> Empty => new() { Items = [], TotalCount = 0 };
}

// Query parameters shared by every list endpoint.
public record PagedRequestDto
{
    private const int MaxPageSize = 100;

    public int SkipCount { get; init; }

    private readonly int _maxResultCount = 20;
    public int MaxResultCount
    {
        get => _maxResultCount;
        init => _maxResultCount = value is <= 0 or > MaxPageSize ? 20 : value;
    }
}
