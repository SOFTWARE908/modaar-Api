namespace modaar.api.Features.Users.Entities
{
    public class Broker
    {

        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;


        public string? SubtitleAr { get; set; }
        public string? SubtitleEn { get; set; }

        public string? AboutAr { get; set; }
        public string? AboutEn { get; set; }

        public string? LicenseNumber { get; set; }

        // The brokerage firm (منشأة الوساطة العقارية) on Ejar contracts. Filled by the admin panel.
        public string? CompanyName { get; set; }
        public string? CommercialRegistrationNumber { get; set; }   // 10 digits

        // The year the brokerage started; "years of experience" is counted from it so it never
        // goes stale. Set the same way as the rest of the profile (no edit endpoint yet).
        public int? ExperienceSinceYear { get; set; }
        public bool IsVerified { get; set; }

        // Rollup over published reviews, recalculated whenever a review is written. Kept on the row so
        // the list and summary cards don't have to aggregate BrokerReviews on every read.
        public decimal RatingAverage { get; set; }
        public int ReviewsCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Display-only lists, owned and stored as JSON columns: nothing queries or joins them, they
        // are always loaded with their broker, and they are edited as a whole list or not at all.
        public List<BrokerCoverageArea> CoverageAreas { get; set; } = [];
        public List<BrokerStat> Stats { get; set; } = [];
    }


    public class BrokerCoverageArea
    {
        // Optional handle for a future city/district lookup; the names are what the app renders today.
        public string? AreaCode { get; set; }
        public string NameAr { get; set; } = null!;
        public string NameEn { get; set; } = null!;
        public int SortOrder { get; set; }
    }

    public class BrokerStat
    {
        public string TitleAr { get; set; } = null!;
        public string TitleEn { get; set; } = null!;

        // A display string, not a number: "98%", "120+".
        public string Value { get; set; } = null!;

        public string? Icon { get; set; }
        public int SortOrder { get; set; }
    }
}

namespace modaar.api.Features.Users.Entities
{
    // A broker working for a brokerage (اسم الموظف الوسيط on the Ejar contract). Not an account:
    // just a name the admin keeps per brokerage and picks on a contract.
    public class BrokerEmployee
    {
        public Guid Id { get; set; }
        public Guid BrokerId { get; set; }
        public Broker Broker { get; set; } = null!;

        public string FullName { get; set; } = null!;

        // Removing someone who left keeps their name on the contracts they signed.
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
