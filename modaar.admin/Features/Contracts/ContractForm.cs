using System.ComponentModel.DataAnnotations;
using modaar.api.Features.Contracts.Enums;

namespace modaar.admin.Features.Contracts;

// The "add contract" form: the Ejar contract sheet for one unit. The lessor is always the unit's
// owner; the tenant is found by phone or created.
public class ContractForm
{
    private const string Req = "هذا الحقل مطلوب.";
    public const string IdNumberPattern = @"^\d{10}$";
    public const string PhonePattern = @"^0?5\d{8}$";

    // ── بيانات العقد ──
    [Display(Name = "رقم سجل العقد")]
    [Required(ErrorMessage = Req), RegularExpression(@"^\d{11}(\s*/\s*\d+-\d+)?$", ErrorMessage = "11 رقم، وممكن بعدها رقم النسخة: 10131452216 / 1-0")]
    public string? ContractNumber { get; set; }

    [Display(Name = "نوع العقد")]
    [Required(ErrorMessage = Req)]
    public Guid? ContractTypeId { get; set; }

    [Display(Name = "مكان إبرام العقد")]
    [Required(ErrorMessage = Req)]
    public Guid? ContractPlaceId { get; set; }

    [Display(Name = "تاريخ إبرام العقد")]
    [Required(ErrorMessage = Req)]
    public DateOnly? SignedOn { get; set; }

    [Display(Name = "تاريخ بداية مدة الإيجار")]
    [Required(ErrorMessage = Req)]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "تاريخ نهاية مدة الإيجار")]
    [Required(ErrorMessage = Req)]
    public DateOnly? EndDate { get; set; }

    // ── المؤجر (مالك الوحدة) ──
    [Display(Name = "اسم المؤجر")]
    [Required(ErrorMessage = Req), StringLength(150, ErrorMessage = "150 حرف كحد أقصى.")]
    public string? LessorName { get; set; }

    [Display(Name = "الجنسية")]
    [Required(ErrorMessage = Req)]
    public Guid? LessorNationalityId { get; set; }

    [Display(Name = "نوع الهوية")]
    [Required(ErrorMessage = Req)]
    public Guid? LessorIdTypeId { get; set; }

    [Display(Name = "رقم الهوية")]
    [Required(ErrorMessage = Req), RegularExpression(IdNumberPattern, ErrorMessage = "10 أرقام.")]
    public string? LessorIdNumber { get; set; }

    public bool HasLessorRepresentative { get; set; }
    public RepresentativeForm LessorRep { get; set; } = new();

    // ── المستأجر ──
    [Display(Name = "جوال المستأجر")]
    [Required(ErrorMessage = Req), RegularExpression(PhonePattern, ErrorMessage = "رقم جوال سعودي يبدأ بـ 5 (9 أرقام).")]
    public string? TenantPhone { get; set; }

    [Display(Name = "اسم المستأجر")]
    [Required(ErrorMessage = Req), StringLength(150, ErrorMessage = "150 حرف كحد أقصى.")]
    public string? TenantName { get; set; }

    [Display(Name = "الجنسية")]
    [Required(ErrorMessage = Req)]
    public Guid? TenantNationalityId { get; set; }

    [Display(Name = "نوع الهوية")]
    [Required(ErrorMessage = Req)]
    public Guid? TenantIdTypeId { get; set; }

    [Display(Name = "رقم الهوية")]
    [Required(ErrorMessage = Req), RegularExpression(IdNumberPattern, ErrorMessage = "10 أرقام.")]
    public string? TenantIdNumber { get; set; }

    public bool HasTenantRepresentative { get; set; }
    public RepresentativeForm TenantRep { get; set; } = new();

    // ── منشأة الوساطة (اختياري) ──
    [Display(Name = "الوسيط (الموظف)")]
    public Guid? BrokerEmployeeId { get; set; }

    // ── البيانات المالية ──
    [Display(Name = "قيمة الإيجار السنوي")]
    [Required(ErrorMessage = Req), Range(typeof(decimal), "1", "100000000", ErrorMessage = "رقم أكبر من 0.")]
    public decimal? AnnualRent { get; set; }

    [Display(Name = "أجرة المياه السنوية")]
    [Required(ErrorMessage = Req), Range(typeof(decimal), "0", "10000000", ErrorMessage = "رقم من 0.")]
    public decimal? AnnualWaterFee { get; set; }

    [Display(Name = "مبلغ الضمان")]
    [Required(ErrorMessage = Req), Range(typeof(decimal), "0", "100000000", ErrorMessage = "رقم من 0.")]
    public decimal? SecurityDeposit { get; set; }

    [Display(Name = "دورة سداد الإيجار")]
    [Required(ErrorMessage = Req)]
    public PaymentFrequency? PaymentFrequency { get; set; }

    [Display(Name = "قنوات الدفع المتاحة")]
    [MinLength(1, ErrorMessage = "اختر قناة دفع واحدة على الأقل.")]
    public List<Guid> PaymentChannelIds { get; set; } = [];
}

// ممثل المؤجر / ممثل المستأجر. Checked only when the "has representative" box is ticked.
public class RepresentativeForm
{
    [Display(Name = "اسم الممثل")]
    [StringLength(150, ErrorMessage = "150 حرف كحد أقصى.")]
    public string? FullName { get; set; }

    [Display(Name = "الجنسية")]
    public Guid? NationalityId { get; set; }

    [Display(Name = "نوع الهوية")]
    public Guid? IdTypeId { get; set; }

    [Display(Name = "رقم الهوية")]
    public string? IdNumber { get; set; }

    [Display(Name = "رقم الجوال")]
    public string? Phone { get; set; }
}

public static class FrequencyLabels
{
    public static string Of(PaymentFrequency f) => f switch
    {
        PaymentFrequency.Monthly => "شهري",
        PaymentFrequency.Quarterly => "ربع سنوي",
        PaymentFrequency.SemiAnnual => "نصف سنوي",
        _ => "سنوي"
    };

    public static int Months(PaymentFrequency f) => f switch
    {
        PaymentFrequency.Monthly => 1,
        PaymentFrequency.Quarterly => 3,
        PaymentFrequency.SemiAnnual => 6,
        _ => 12
    };
}

public static class StatusLabels
{
    public static string Of(ContractStatus s) => s switch
    {
        ContractStatus.Active => "ساري",
        ContractStatus.Expired => "منتهي",
        _ => "منهي مبكراً"
    };
}
