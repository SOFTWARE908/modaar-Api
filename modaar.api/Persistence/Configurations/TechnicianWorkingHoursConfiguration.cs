using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Maintenance.Entities;

namespace modaar.api.Persistence.Configurations;

public class TechnicianWorkingHoursConfiguration : IEntityTypeConfiguration<TechnicianWorkingHours>
{
    public void Configure(EntityTypeBuilder<TechnicianWorkingHours> b)
    {
        b.ToTable("TechnicianWorkingHours");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10).IsRequired();

        b.HasOne<Technician>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);

        // One row per working day.
        b.HasIndex(x => new { x.TechnicianId, x.DayOfWeek }).IsUnique();
    }
}
