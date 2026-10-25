using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class BuildingOwnerSubscriptionConfiguration : IEntityTypeConfiguration<BuildingOwnerSubscription>
{
    public void Configure(EntityTypeBuilder<BuildingOwnerSubscription> b)
    {
        b.ToTable("BuildingOwnerSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        // No HasDefaultValue: SelfManaged is the enum's 0, so EF would treat it as "unset" and let a
        // database default overwrite it.
        b.Property(x => x.Plan).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AutoApproveLimit).HasPrecision(18, 2);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.BuildingId, x.OwnerUserId }).IsUnique();
        b.HasIndex(x => x.OwnerUserId);

        // NoAction: Users already cascade into Buildings (CreatedByUserId), and a second path is
        // what SQL Server rejects.
        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
