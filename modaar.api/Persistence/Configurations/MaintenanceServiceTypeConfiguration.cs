using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Maintenance.Entities;

namespace modaar.api.Persistence.Configurations
{
    public class MaintenanceServiceTypeConfiguration : IEntityTypeConfiguration<MaintenanceServiceType>
    {
        public void Configure(EntityTypeBuilder<MaintenanceServiceType> b)
        {
            b.ToTable("MaintenanceServiceTypes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Code).HasMaxLength(40).IsRequired();
            b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
            b.Property(x => x.IconUrl).HasMaxLength(500);

            // Unique across active and inactive rows alike: reusing a retired code would make the
            // client's icon mapping point at the wrong category.
            b.HasIndex(x => x.Code).IsUnique();

            // The picker reads active rows in order, which is this index exactly.
            b.HasIndex(x => new { x.IsActive, x.SortOrder });
        }
    }
}
