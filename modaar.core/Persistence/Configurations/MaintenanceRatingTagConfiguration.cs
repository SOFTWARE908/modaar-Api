using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Maintenance.Entities;

namespace modaar.api.Persistence.Configurations
{
    public class MaintenanceRatingTagConfiguration : IEntityTypeConfiguration<MaintenanceRatingTag>
    {
        public void Configure(EntityTypeBuilder<MaintenanceRatingTag> b)
        {
            b.ToTable("MaintenanceRatingTags");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.LabelAr).HasMaxLength(100).IsRequired();
            b.Property(x => x.LabelEn).HasMaxLength(100).IsRequired();

            b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            // Unique across active and retired rows: reusing a key would change what old ratings mean.
            b.HasIndex(x => x.Key).IsUnique();

            // The rating screen reads active rows in order.
            b.HasIndex(x => new { x.IsActive, x.SortOrder });
        }
    }
}
