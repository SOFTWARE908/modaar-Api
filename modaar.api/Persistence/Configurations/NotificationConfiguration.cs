using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Notifications.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(30);

        b.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
        b.Property(x => x.BodyAr).HasMaxLength(1000).IsRequired();
        b.Property(x => x.BodyEn).HasMaxLength(1000).IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // The list: a user's notifications, newest first, optionally by type.
        b.HasIndex(x => new { x.UserId, x.CreatedAt });

        // The bell: counting unread rows only, so the index holds only those.
        b.HasIndex(x => x.UserId).HasFilter("[IsRead] = 0").HasDatabaseName("IX_Notifications_UserId_Unread");

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SentReminderConfiguration : IEntityTypeConfiguration<SentReminder>
{
    public void Configure(EntityTypeBuilder<SentReminder> b)
    {
        b.ToTable("SentReminders");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
    }
}
