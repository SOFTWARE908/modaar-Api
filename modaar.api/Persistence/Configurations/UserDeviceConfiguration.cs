using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> b)
    {
        b.ToTable("UserDevices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.DeviceId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Platform).HasMaxLength(20);
        b.Property(x => x.BiometricPublicKey).HasMaxLength(500);
        b.Property(x => x.BiometricChallenge).HasMaxLength(100);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // One row per user per phone.
        b.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();

        // Biometric login arrives with only the device id; this finds the row.
        b.HasIndex(x => x.DeviceId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
