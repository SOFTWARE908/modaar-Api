using modaar.api.Features.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Authentication.Entities;
namespace modaar.api.Persistence.Configurations
{

    public class BrokerConfiguration : IEntityTypeConfiguration<Broker>
    {
        public void Configure(EntityTypeBuilder<Broker> b)
        {
            b.ToTable("Brokers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.SubtitleAr).HasMaxLength(300);
            b.Property(x => x.SubtitleEn).HasMaxLength(300);

            b.Property(x => x.AboutAr).HasMaxLength(4000);
            b.Property(x => x.AboutEn).HasMaxLength(4000);

            b.Property(x => x.LicenseNumber).HasMaxLength(50);

            b.Property(x => x.RatingAverage).HasPrecision(3, 2);

            b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            // One brokerage per account. No IsDeleted filter here: the row's lifetime follows the
            // user's, and a soft-deleted user keeps its brokerage rather than freeing it up.
            b.HasIndex(x => x.UserId).IsUnique();

            b.HasIndex(x => x.LicenseNumber)
                .IsUnique()
                .HasFilter("[LicenseNumber] IS NOT NULL");

            b.HasOne<User>(x=>x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Stored as JSON columns: read and written whole, never filtered on.
            b.OwnsMany(x => x.Services, s =>
            {
                s.ToJson();
                s.Property(p => p.NameAr).IsRequired();
                s.Property(p => p.NameEn).IsRequired();
            });

            b.OwnsMany(x => x.CoverageAreas, s =>
            {
                s.ToJson();
                s.Property(p => p.NameAr).IsRequired();
                s.Property(p => p.NameEn).IsRequired();
            });

            b.OwnsMany(x => x.Stats, s =>
            {
                s.ToJson();
                s.Property(p => p.TitleAr).IsRequired();
                s.Property(p => p.TitleEn).IsRequired();
                s.Property(p => p.Value).IsRequired();
            });
        }
    }
}
