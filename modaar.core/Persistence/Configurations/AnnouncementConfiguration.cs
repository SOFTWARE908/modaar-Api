using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Announcements.Entities;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

// NoAction on the user/property links: Users already cascade into Properties, and a second path
// into these tables is what SQL Server rejects.
public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.ToTable("Announcements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.PrimitiveCollection(x => x.PropertyIds).HasMaxLength(4000);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Recipients).WithOne().HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AnnouncementRecipientConfiguration : IEntityTypeConfiguration<AnnouncementRecipient>
{
    public void Configure(EntityTypeBuilder<AnnouncementRecipient> b)
    {
        b.ToTable("AnnouncementRecipients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.HasIndex(x => new { x.AnnouncementId, x.TenantUserId }).IsUnique();
        b.HasIndex(x => x.TenantUserId);

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AnnouncementCategoryConfiguration : IEntityTypeConfiguration<AnnouncementCategory>
{
    public void Configure(EntityTypeBuilder<AnnouncementCategory> b)
    {
        b.ToTable("AnnouncementCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.IsActive, x.SortOrder });

        // Starting set; the admin adds, renames and retires from here.
        var at = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000001", "elevator_maintenance", "صيانة المصعد", "Elevator maintenance", 10, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000002", "stairs_cleaning", "غسيل السلالم", "Stairwell cleaning", 20, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000003", "painting", "دهان", "Painting", 30, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000004", "water_outage", "انقطاع المياه", "Water outage", 40, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000005", "power_outage", "انقطاع الكهرباء", "Power outage", 50, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000006", "water_tank_cleaning", "تنظيف خزانات المياه", "Water tank cleaning", 60, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000007", "pest_control", "مكافحة الحشرات", "Pest control", 70, at),
            Seed("a7c1e7a0-0001-4c1a-9a01-000000000008", "general", "إعلان عام", "General notice", 100, at));
    }

    private static AnnouncementCategory Seed(string id, string code, string ar, string en, int order, DateTimeOffset at) =>
        new() { Id = Guid.Parse(id), Code = code, NameAr = ar, NameEn = en, SortOrder = order, IsActive = true, CreatedAt = at, UpdatedAt = at };
}
