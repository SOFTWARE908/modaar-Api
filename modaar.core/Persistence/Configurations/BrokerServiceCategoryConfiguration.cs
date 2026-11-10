using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class BrokerServiceCategoryConfiguration : IEntityTypeConfiguration<BrokerServiceCategory>
{
    private const string Icons = "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/";

    public void Configure(EntityTypeBuilder<BrokerServiceCategory> b)
    {
        b.ToTable("BrokerServiceCategories");
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

        // The services in the marketplace design; the admin adds, renames and retires from here.
        var at = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            Seed("c3a1f0d2-0001-4b6e-9a01-000000000001", "property_management", "إدارة أملاك", "Property management", "building-2", 10, at),
            Seed("c3a1f0d2-0001-4b6e-9a01-000000000002", "marketing_leasing", "تسويق وتأجير", "Marketing and leasing", "megaphone", 20, at),
            Seed("c3a1f0d2-0001-4b6e-9a01-000000000003", "commercial", "تجاري", "Commercial", "store", 30, at),
            Seed("c3a1f0d2-0001-4b6e-9a01-000000000004", "maintenance", "صيانة", "Maintenance", "wrench", 40, at));
    }

    private static BrokerServiceCategory Seed(string id, string code, string ar, string en, string icon, int order, DateTimeOffset at) =>
        new()
        {
            Id = Guid.Parse(id), Code = code, NameAr = ar, NameEn = en, IconUrl = $"{Icons}{icon}.svg",
            SortOrder = order, IsActive = true, CreatedAt = at, UpdatedAt = at
        };
}

public class BrokerOfferedServiceConfiguration : IEntityTypeConfiguration<BrokerOfferedService>
{
    public void Configure(EntityTypeBuilder<BrokerOfferedService> b)
    {
        b.ToTable("BrokerOfferedServices");
        b.HasKey(x => new { x.BrokerId, x.CategoryId });
        b.HasIndex(x => x.CategoryId);

        b.HasOne<Broker>().WithMany().HasForeignKey(x => x.BrokerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<BrokerServiceCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
