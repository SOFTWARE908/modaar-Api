namespace modaar.api.Features.Lookups.Entities;

// A named list of options, e.g. "nationality" or "id_type". One table for all the small fixed lists
// the Ejar contract sheet picks from, instead of a table each.
public class LookupCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
}

// One option of a list. Rows point at it by Id; Code is the stable name code can rely on.
public class Lookup
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public LookupCategory Category { get; set; } = null!;

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public int SortOrder { get; set; }

    // Retired options stay readable on old rows but are no longer offered.
    public bool IsActive { get; set; } = true;
}

// The category codes code refers to.
public static class LookupCategories
{
    public const string Nationality = "nationality";
    public const string IdType = "id_type";
    public const string ContractPlace = "contract_place";
    public const string ContractType = "contract_type";
    public const string PaymentChannel = "payment_channel";
}
