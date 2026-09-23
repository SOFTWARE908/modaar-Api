using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class BrokerManagementRequestConfiguration : IEntityTypeConfiguration<BrokerManagementRequest>
{
    public void Configure(EntityTypeBuilder<BrokerManagementRequest> b)
    {
        b.ToTable("BrokerManagementRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.DecisionReason).HasMaxLength(1000);

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.BrokerId, x.Status });
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });

        // One open ask per owner per brokerage, so tapping the button twice doesn't queue
        // duplicates.
        b.HasIndex(x => new { x.OwnerUserId, x.BrokerId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        // NoAction throughout: Users already cascades into Brokers, and a second path into this
        // table is what SQL Server rejects.
        b.HasOne<Broker>()
            .WithMany()
            .HasForeignKey(x => x.BrokerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.PrimitiveCollection(x => x.PropertyIds).HasMaxLength(4000);
    }
}
