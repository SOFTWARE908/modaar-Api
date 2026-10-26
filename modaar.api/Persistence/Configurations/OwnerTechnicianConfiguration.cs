using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class OwnerTechnicianConfiguration : IEntityTypeConfiguration<OwnerTechnician>
{
    public void Configure(EntityTypeBuilder<OwnerTechnician> b)
    {
        b.ToTable("OwnerTechnicians");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.OwnerUserId, x.TechnicianId }).IsUnique();
        b.HasIndex(x => x.TechnicianId);

        // NoAction: Users already cascade into Technicians, and a second path is what SQL Server
        // rejects.
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.NoAction);
    }
}
