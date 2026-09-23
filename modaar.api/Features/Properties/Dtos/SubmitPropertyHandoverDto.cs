using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Dtos;

public record SubmitPropertyHandoverDto
{
    // Replaces the whole gallery: the list is one JSON column with no per-image id to patch
    // against.
    public required IReadOnlyList<string> Images { get; init; }

    public string? Description { get; init; }

    public string? SignatureUrl { get; init; }

    // Sent by the caller, as the spec has it. The server stores what it is given rather than
    // deriving it.
    public PropertyHandoverStatus? Status { get; init; }

    public DateOnly? HandoverDate { get; init; }
}
