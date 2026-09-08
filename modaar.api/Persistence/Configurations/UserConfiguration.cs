using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();

        b.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
        b.Property(u => u.CountryCode).HasMaxLength(8).IsRequired();

        b.Property(u => u.FullName).HasMaxLength(150);
        b.Property(u => u.Email).HasMaxLength(256);
        b.Property(u => u.NationalId).HasMaxLength(50);
        b.Property(u => u.PasswordHash).HasMaxLength(500);
        b.Property(u => u.ProfileImageUrl).HasMaxLength(500);

        b.Property(u => u.AccountType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(u => u.ProfileStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(u => u.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(u => u.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // The mobile number is the identity, so this is the one index that must always hold.
        // Filtered on IsDeleted so a soft-deleted user frees their number up again.
        b.HasIndex(u => new { u.CountryCode, u.PhoneNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Email and nationalId arrive later, so the uniqueness filter must also skip the NULLs —
        // SQL Server would otherwise allow only a single row with no email at all.
        b.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Email] IS NOT NULL");

        b.HasIndex(u => u.NationalId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [NationalId] IS NOT NULL");
    }
}
