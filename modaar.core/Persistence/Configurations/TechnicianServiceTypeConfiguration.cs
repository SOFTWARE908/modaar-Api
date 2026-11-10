using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Maintenance.Entities;

namespace modaar.api.Persistence.Configurations
{
    public class TechnicianServiceTypeConfiguration : IEntityTypeConfiguration<TechnicianServiceType>
    {
        public void Configure(EntityTypeBuilder<TechnicianServiceType> b)
        {
            b.ToTable("TechnicianServiceTypes");
            b.HasKey(x => new { x.TechnicianId, x.ServiceTypeId });

            // Cascade from the technician: their skills are theirs and mean nothing without them.
            b.HasOne<Technician>()
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<MaintenanceServiceType>()
                .WithMany()
                .HasForeignKey(x => x.ServiceTypeId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
