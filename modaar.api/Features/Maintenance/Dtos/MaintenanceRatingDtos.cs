using System.Text.Json.Serialization;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance.Dtos
{
    // Send the four criteria (the current form) or Stars alone (older clients). With the four,
    // Stars is ignored and derived as their rounded average.
    public record MaintenanceRatingSubmitDto
    {
        public int? TechnicianRating { get; init; }
        public int? RepairQualityRating { get; init; }
        public int? ResponseSpeedRating { get; init; }
        public int? BrokerCommunicationRating { get; init; }

        public int? Stars { get; init; }

        // Keys from GET /maintenance-requests/rating-tags. Checked against active tags on submit.
        public IReadOnlyList<string>? TagKeys { get; init; }
        public string? Comment { get; init; }

        // Not in the current design, still accepted so older builds keep working. Both end up in
        // one column on the entity; only one of the two may be sent.
        public int? PresetTipSar { get; init; }
        public decimal? CustomTipSar { get; init; }

        [JsonIgnore]
        public bool HasCriteria =>
            TechnicianRating is not null || RepairQualityRating is not null ||
            ResponseSpeedRating is not null || BrokerCommunicationRating is not null;
    }

    public record MaintenanceRatingTagDto
    {
        public required string Key { get; init; }
        public required string LabelAr { get; init; }
        public required string LabelEn { get; init; }
    }

    public record NeedHelpRequestDto
    {
        public required NeedHelpIssueType IssueType { get; init; }
        public string? Description { get; init; }
        public required NeedHelpContactMethod ContactMethod { get; init; }
    }
}
