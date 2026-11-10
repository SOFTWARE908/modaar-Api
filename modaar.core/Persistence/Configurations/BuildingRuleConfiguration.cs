using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Buildings.Entities;

namespace modaar.api.Persistence.Configurations;

public class BuildingRuleCategoryConfiguration : IEntityTypeConfiguration<BuildingRuleCategory>
{
    private const string Icons = "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/";

    public void Configure(EntityTypeBuilder<BuildingRuleCategory> b)
    {
        b.ToTable("BuildingRuleCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        b.Property(x => x.IconUrl).HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.IsActive, x.SortOrder });

        // Starting set, the sections in the design; the admin adds, renames and retires from here.
        var at = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000001", "quiet_hours", "أوقات الهدوء", "Quiet hours", "moon", 10, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000002", "waste_recycling", "النفايات وإعادة التدوير", "Waste and recycling", "trash-2", 20, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000003", "parking", "المواقف", "Parking", "car", 30, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000004", "shared_facilities", "المرافق المشتركة", "Shared facilities", "dumbbell", 40, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000005", "visitors", "الزوار", "Visitors", "users", 50, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000006", "safety", "السلامة", "Safety", "shield", 60, at),
            Seed("b7d2e8b0-0001-4d2b-8b02-000000000007", "general", "عام", "General", "clipboard-list", 100, at));
    }

    private static BuildingRuleCategory Seed(string id, string code, string ar, string en, string icon, int order, DateTimeOffset at) =>
        new()
        {
            Id = Guid.Parse(id), Code = code, NameAr = ar, NameEn = en, IconUrl = $"{Icons}{icon}.svg",
            SortOrder = order, IsActive = true, CreatedAt = at, UpdatedAt = at
        };
}

public class BuildingRuleConfiguration : IEntityTypeConfiguration<BuildingRule>
{
    public void Configure(EntityTypeBuilder<BuildingRule> b)
    {
        b.ToTable("BuildingRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Text).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.BuildingId, x.CategoryId, x.SortOrder });

        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<BuildingRuleCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BuildingContactConfiguration : IEntityTypeConfiguration<BuildingContact>
{
    public void Configure(EntityTypeBuilder<BuildingContact> b)
    {
        b.ToTable("BuildingContacts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Note).HasMaxLength(100);
        b.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.BuildingId, x.SortOrder });

        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
    }
}
