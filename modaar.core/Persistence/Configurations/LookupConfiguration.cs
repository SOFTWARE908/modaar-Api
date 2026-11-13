using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Lookups.Entities;

namespace modaar.api.Persistence.Configurations;

public class LookupCategoryConfiguration : IEntityTypeConfiguration<LookupCategory>
{
    public void Configure(EntityTypeBuilder<LookupCategory> b)
    {
        b.ToTable("LookupCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();

        b.HasData(LookupSeed.Categories.Select(c => new LookupCategory
        {
            Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn
        }));
    }
}

public class LookupConfiguration : IEntityTypeConfiguration<Lookup>
{
    public void Configure(EntityTypeBuilder<Lookup> b)
    {
        b.ToTable("Lookups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(150).IsRequired();

        b.HasIndex(x => new { x.CategoryId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.CategoryId, x.IsActive, x.SortOrder });
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        b.HasData(LookupSeed.Options());
    }
}

// The options from the Ejar contract sheet. Ids are fixed so code and other seeds can point at them.
public static class LookupSeed
{
    public record Cat(Guid Id, string Code, string NameAr, string NameEn, string[][] Options);

    private static Guid CatId(int n) => Guid.Parse($"1c000000-0000-4000-8000-{n:D12}");

    public static readonly Cat[] Categories =
    [
        new(CatId(1), LookupCategories.Nationality, "الجنسية", "Nationality",
        [
            ["sa", "المملكة العربية السعودية", "Saudi Arabia"],
            ["af", "أفغانستان", "Afghanistan"], ["al", "ألبانيا", "Albania"], ["dz", "الجزائر", "Algeria"],
            ["ar", "الأرجنتين", "Argentina"], ["am", "أرمينيا", "Armenia"], ["au", "أستراليا", "Australia"],
            ["at", "النمسا", "Austria"], ["az", "أذربيجان", "Azerbaijan"], ["bh", "البحرين", "Bahrain"],
            ["bd", "بنغلاديش", "Bangladesh"], ["be", "بلجيكا", "Belgium"], ["ba", "البوسنة والهرسك", "Bosnia and Herzegovina"],
            ["bf", "بوركينا فاسو", "Burkina Faso"], ["bi", "بوروندي", "Burundi"], ["cm", "الكاميرون", "Cameroon"],
            ["ca", "كندا", "Canada"], ["cv", "الرأس الأخضر", "Cape Verde"], ["td", "تشاد", "Chad"],
            ["cn", "الصين", "China"], ["co", "كولومبيا", "Colombia"], ["km", "جزر القمر", "Comoros"],
            ["cg", "الكونغو", "Congo"], ["cr", "كوستاريكا", "Costa Rica"], ["ci", "ساحل العاج", "Ivory Coast"],
            ["hr", "كرواتيا", "Croatia"], ["cy", "قبرص", "Cyprus"], ["cz", "التشيك", "Czech Republic"],
            ["dk", "الدنمارك", "Denmark"], ["dj", "جيبوتي", "Djibouti"], ["ec", "الإكوادور", "Ecuador"],
            ["eg", "مصر", "Egypt"], ["sv", "السلفادور", "El Salvador"], ["er", "إريتريا", "Eritrea"],
            ["et", "إثيوبيا", "Ethiopia"], ["fi", "فنلندا", "Finland"], ["fr", "فرنسا", "France"],
            ["gm", "غامبيا", "Gambia"], ["ge", "جورجيا", "Georgia"], ["de", "ألمانيا", "Germany"],
            ["gh", "غانا", "Ghana"], ["gr", "اليونان", "Greece"], ["gn", "غينيا", "Guinea"],
            ["hu", "المجر", "Hungary"], ["is", "آيسلندا", "Iceland"], ["in", "الهند", "India"],
            ["id", "إندونيسيا", "Indonesia"], ["ir", "إيران", "Iran"], ["iq", "العراق", "Iraq"],
            ["ie", "أيرلندا", "Ireland"], ["it", "إيطاليا", "Italy"], ["jm", "جامايكا", "Jamaica"],
            ["jp", "اليابان", "Japan"], ["jo", "الأردن", "Jordan"], ["kz", "كازاخستان", "Kazakhstan"],
            ["ke", "كينيا", "Kenya"], ["kw", "الكويت", "Kuwait"], ["kg", "قيرغيزستان", "Kyrgyzstan"],
            ["lb", "لبنان", "Lebanon"], ["ly", "ليبيا", "Libya"], ["mg", "مدغشقر", "Madagascar"],
            ["my", "ماليزيا", "Malaysia"], ["mv", "جزر المالديف", "Maldives"], ["mr", "موريتانيا", "Mauritania"],
            ["mu", "موريشيوس", "Mauritius"], ["ma", "المغرب", "Morocco"], ["mz", "موزمبيق", "Mozambique"],
            ["np", "نيبال", "Nepal"], ["nl", "هولندا", "Netherlands"], ["nz", "نيوزيلندا", "New Zealand"],
            ["ne", "النيجر", "Niger"], ["ng", "نيجيريا", "Nigeria"], ["no", "النرويج", "Norway"],
            ["om", "عُمان", "Oman"], ["pk", "باكستان", "Pakistan"], ["ps", "فلسطين", "Palestine"],
            ["pa", "بنما", "Panama"], ["ph", "الفلبين", "Philippines"], ["pl", "بولندا", "Poland"],
            ["pt", "البرتغال", "Portugal"], ["qa", "قطر", "Qatar"], ["ro", "رومانيا", "Romania"],
            ["ru", "روسيا", "Russia"], ["rw", "رواندا", "Rwanda"], ["sn", "السنغال", "Senegal"],
            ["rs", "صربيا", "Serbia"], ["sg", "سنغافورة", "Singapore"], ["sk", "سلوفاكيا", "Slovakia"],
            ["si", "سلوفينيا", "Slovenia"], ["sb", "جزر سليمان", "Solomon Islands"], ["so", "الصومال", "Somalia"],
            ["za", "جنوب أفريقيا", "South Africa"], ["kr", "كوريا الجنوبية", "South Korea"], ["es", "إسبانيا", "Spain"],
            ["lk", "سريلانكا", "Sri Lanka"], ["sd", "السودان", "Sudan"], ["se", "السويد", "Sweden"],
            ["ch", "سويسرا", "Switzerland"], ["sy", "سوريا", "Syria"], ["tj", "طاجيكستان", "Tajikistan"],
            ["th", "تايلاند", "Thailand"], ["tn", "تونس", "Tunisia"], ["tr", "تركيا", "Turkey"],
            ["ug", "أوغندا", "Uganda"], ["ua", "أوكرانيا", "Ukraine"], ["ae", "الإمارات العربية المتحدة", "United Arab Emirates"],
            ["gb", "المملكة المتحدة", "United Kingdom"], ["us", "الولايات المتحدة الأمريكية", "United States"], ["uy", "أوروغواي", "Uruguay"],
            ["uz", "أوزبكستان", "Uzbekistan"], ["vn", "فيتنام", "Vietnam"], ["ye", "اليمن", "Yemen"]
        ]),
        new(CatId(2), LookupCategories.IdType, "نوع الهوية", "ID type",
        [
            ["national_id", "هوية وطنية", "National ID"],
            ["premium_residency", "إقامة مميزة", "Premium residency"],
            ["resident_id", "هوية مقيم", "Resident ID"],
            ["gcc_id", "هوية مواطني دول مجلس التعاون الخليجي", "GCC citizen ID"],
            ["passport", "جواز سفر", "Passport"],
            ["other", "أخرى", "Other"]
        ]),
        new(CatId(3), LookupCategories.ContractPlace, "مكان إبرام العقد", "Place of signing",
        [
            ["riyadh", "الرياض", "Riyadh"],
            ["other", "أخرى", "Other"]
        ]),
        new(CatId(4), LookupCategories.ContractType, "نوع العقد", "Contract type",
        [
            ["new", "جديد", "New"],
            ["renewal", "تجديد", "Renewal"]
        ]),
        new(CatId(5), LookupCategories.PaymentChannel, "قنوات الدفع", "Payment channels",
        [
            ["mada", "مدى", "Mada"],
            ["sadad", "سداد", "SADAD"]
        ])
    ];

    // Lookup ids: category number in the first group, position in the last.
    public static Guid OptionId(int category, int position) =>
        Guid.Parse($"1d{category:D6}-0000-4000-8000-{position:D12}");

    public static IEnumerable<Lookup> Options() =>
        Categories.SelectMany((c, ci) => c.Options.Select((o, oi) => new Lookup
        {
            Id = OptionId(ci + 1, oi + 1),
            CategoryId = c.Id,
            Code = o[0], NameAr = o[1], NameEn = o[2],
            SortOrder = (oi + 1) * 10,
            IsActive = true
        }));
}

public class BrokerEmployeeConfiguration : IEntityTypeConfiguration<modaar.api.Features.Users.Entities.BrokerEmployee>
{
    public void Configure(EntityTypeBuilder<modaar.api.Features.Users.Entities.BrokerEmployee> b)
    {
        b.ToTable("BrokerEmployees");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.HasIndex(x => new { x.BrokerId, x.IsActive });
        b.HasOne(x => x.Broker).WithMany().HasForeignKey(x => x.BrokerId).OnDelete(DeleteBehavior.Cascade);
    }
}
