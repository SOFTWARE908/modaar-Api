using System.ComponentModel.DataAnnotations;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Properties.Enums;

namespace modaar.admin.Features.Buildings;

// The "add building" form: the building and title-deed sections of the Ejar sheet.
public class BuildingForm
{
    private const string Req = "هذا الحقل مطلوب.";

    [Display(Name = "اسم المبنى")]
    [Required(ErrorMessage = Req), StringLength(200, ErrorMessage = "200 حرف كحد أقصى.")]
    public string? Name { get; set; }

    [Display(Name = "نوع بناء العقار")]
    [Required(ErrorMessage = Req)]
    public BuildingType? Type { get; set; }

    [Display(Name = "الغرض من الاستخدام")]
    [Required(ErrorMessage = Req), StringLength(100, ErrorMessage = "100 حرف كحد أقصى.")]
    public string? UsagePurpose { get; set; }

    [Display(Name = "عدد الطوابق")]
    [Required(ErrorMessage = Req), Range(1, 4, ErrorMessage = "من 1 إلى 4 طوابق.")]
    public int? FloorsCount { get; set; }

    [Display(Name = "عدد الوحدات")]
    [Required(ErrorMessage = Req), Range(1, 500, ErrorMessage = "من 1 إلى 500 وحدة.")]
    public int? UnitsCount { get; set; }

    [Display(Name = "عدد المواقف")]
    [Required(ErrorMessage = Req), Range(0, 1000, ErrorMessage = "من 0 إلى 1000.")]
    public int? ParkingSpaces { get; set; }

    [Display(Name = "رقم المبنى")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{4}$", ErrorMessage = "4 أرقام.")]
    public string? BuildingNumber { get; set; }

    [Display(Name = "اسم الشارع")]
    [Required(ErrorMessage = Req), StringLength(150, ErrorMessage = "150 حرف كحد أقصى.")]
    public string? Street { get; set; }

    [Display(Name = "الرقم الإضافي")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{4}$", ErrorMessage = "4 أرقام.")]
    public string? AdditionalNumber { get; set; }

    [Display(Name = "الحي")]
    [Required(ErrorMessage = Req), StringLength(100, ErrorMessage = "100 حرف كحد أقصى.")]
    public string? District { get; set; }

    [Display(Name = "الرمز البريدي")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{5}$", ErrorMessage = "5 أرقام.")]
    public string? PostalCode { get; set; }

    [Display(Name = "المدينة")]
    [Required(ErrorMessage = Req)]
    public Guid? CityId { get; set; }

    [Display(Name = "رقم مستند الملكية (الصك)")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{12}$", ErrorMessage = "12 رقم.")]
    public string? DeedNumber { get; set; }

    [Display(Name = "تاريخ إصدار الصك (هجري)")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "بالشكل 1445-11-12.")]
    public string? DeedIssueDateHijri { get; set; }

    [Display(Name = "نوع الصك")]
    [Required(ErrorMessage = Req)]
    public DeedType? DeedType { get; set; }
}

// The "add unit" form: the rental-unit section of the Ejar sheet, plus who owns it.
public class UnitForm
{
    private const string Req = "هذا الحقل مطلوب.";

    // Saudi mobile, with or without the leading 0. Stored as the app stores it: +966 and 5XXXXXXXX.
    [Display(Name = "جوال المالك")]
    [Required(ErrorMessage = Req), RegularExpression(@"^0?5\d{8}$", ErrorMessage = "رقم جوال سعودي يبدأ بـ 5 (9 أرقام).")]
    public string? OwnerPhone { get; set; }

    // Only needed when no account has that number yet: the panel then creates the owner.
    [Display(Name = "اسم المالك")]
    [StringLength(150, ErrorMessage = "150 حرف كحد أقصى.")]
    public string? OwnerName { get; set; }

    [Display(Name = "نوع الوحدة")]
    [Required(ErrorMessage = Req)]
    public PropertyType? PropertyType { get; set; }

    [Display(Name = "رقم الوحدة")]
    [Required(ErrorMessage = Req), StringLength(20, ErrorMessage = "20 حرف كحد أقصى.")]
    public string? UnitNumber { get; set; }

    [Display(Name = "رقم الطابق")]
    [Required(ErrorMessage = Req), StringLength(20, ErrorMessage = "20 حرف كحد أقصى.")]
    public string? Floor { get; set; }

    [Display(Name = "مساحة الوحدة (م²)")]
    [Required(ErrorMessage = Req), Range(1, 100000, ErrorMessage = "رقم أكبر من 0.")]
    public decimal? Area { get; set; }

    [Display(Name = "خزائن مطبخ مركبة")]
    [Required(ErrorMessage = Req)]
    public bool? HasKitchenCabinets { get; set; }

    [Display(Name = "عدد وحدات التكييف")]
    [Required(ErrorMessage = Req), Range(0, 50, ErrorMessage = "من 0 إلى 50.")]
    public int? AcUnitsCount { get; set; }

    // Required when the unit has at least one AC unit; checked in the controller.
    [Display(Name = "نوع التكييف")]
    [StringLength(100, ErrorMessage = "100 حرف كحد أقصى.")]
    public string? AcType { get; set; }

    [Display(Name = "رقم عداد الكهرباء")]
    [Required(ErrorMessage = Req), RegularExpression(@"^[A-Za-z0-9]{1,30}$", ErrorMessage = "حروف إنجليزية وأرقام فقط.")]
    public string? ElectricityMeterNumber { get; set; }

    [Display(Name = "عدد الغرف")]
    [Range(0, 100, ErrorMessage = "من 0 إلى 100.")]
    public int? RoomsCount { get; set; }

    [Display(Name = "الإيجار السنوي (ريال)")]
    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "رقم من 0 إلى 100,000,000.")]
    public decimal? AnnualRent { get; set; }
}

// Arabic names for the lists the forms offer.
public static class Labels
{
    public static string Of(BuildingType t) => t switch
    {
        BuildingType.ResidentialBuilding => "عمارة سكنية",
        BuildingType.VillaCompound => "مجمع فلل",
        _ => "أخرى"
    };

    public static string Of(DeedType t) => t switch
    {
        DeedType.Electronic => "صك ملكية إلكتروني",
        DeedType.Paper => "صك ملكية ورقي",
        DeedType.HujjatIstihkam => "حجة استحكام",
        DeedType.RealEstateRegistry => "صك السجل العقاري",
        _ => "أخرى"
    };

    public static string Of(PropertyType t) => t switch
    {
        PropertyType.Apartment => "شقة",
        PropertyType.Villa => "فيلا",
        PropertyType.Floor => "دور",
        PropertyType.Studio => "استوديو",
        PropertyType.Duplex => "دوبلكس",
        PropertyType.Office => "مكتب",
        PropertyType.Shop => "محل",
        PropertyType.Warehouse => "مستودع",
        PropertyType.Land => "أرض",
        _ => t.ToString()
    };
}
