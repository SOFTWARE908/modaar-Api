using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> b)
    {
        b.ToTable("Buildings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.AddressLine).HasMaxLength(500);
        b.Property(x => x.District).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.Property(x => x.CoverImageUrl).HasMaxLength(500);
        b.Property(x => x.DeedNumber).HasMaxLength(30);
        b.Property(x => x.DeedIssueDateHijri).HasMaxLength(10);
        b.Property(x => x.DeedType).HasConversion<string>().HasMaxLength(30);

        b.Property(x => x.BuildingNumber).HasMaxLength(10);
        b.Property(x => x.Street).HasMaxLength(150);
        b.Property(x => x.AdditionalNumber).HasMaxLength(10);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.UsagePurpose).HasMaxLength(100);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CityLookup).WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> b)
    {
        b.ToTable("Cities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.IsActive, x.SortOrder });

        // The cities on the Ejar sheet, in its order.
        string[][] cities =
        [
            ["riyadh", "الرياض", "Riyadh"],
            ["makkah", "مكة المكرمة", "Makkah"],
            ["madinah", "المدينة المنورة", "Madinah"],
            ["buraydah", "بريدة", "Buraydah"],
            ["dammam", "الدمام", "Dammam"],
            ["tabuk", "تبوك", "Tabuk"],
            ["hail", "حائل", "Hail"],
            ["arar", "عرعر", "Arar"],
            ["sakaka", "سكاكا", "Sakaka"],
            ["abha", "أبها", "Abha"],
            ["jazan", "جازان", "Jazan"],
            ["najran", "نجران", "Najran"],
            ["al_bahah", "الباحة", "Al Bahah"]
        ];
        b.HasData(cities.Select((c, i) => new City
        {
            Id = Guid.Parse($"c1700000-0000-4000-8000-{i + 1:D12}"),
            Code = c[0], NameAr = c[1], NameEn = c[2], SortOrder = (i + 1) * 10, IsActive = true
        }));
    }
}
