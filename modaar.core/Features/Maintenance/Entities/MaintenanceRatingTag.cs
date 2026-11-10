namespace modaar.api.Features.Maintenance.Entities
{
    // A "what did you like?" chip on the rating screen. Ratings store the Key, not the id, so a
    // rating keeps its meaning after a tag is retired.
    public class MaintenanceRatingTag
    {
        public Guid Id { get; set; }

        // Stable key, e.g. "maintenance_rating.tag_respectful". Never edit one once ratings use it;
        // add a new row instead.
        public string Key { get; set; } = null!;

        public string LabelAr { get; set; } = null!;
        public string LabelEn { get; set; } = null!;

        // Order on the screen. Low first.
        public int SortOrder { get; set; }

        // False takes it off the rating screen and stops new ratings using it. Ratings that already
        // carry it keep it.
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
