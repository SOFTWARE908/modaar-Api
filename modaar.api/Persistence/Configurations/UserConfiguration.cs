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

        b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
        b.Property(u => u.CountryCode).HasMaxLength(8).IsRequired();
        b.Property(u => u.NationalId).HasMaxLength(50).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(500);
        b.Property(u => u.ProfileImageUrl).HasMaxLength(500);

        b.Property(u => u.AccountType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(u => u.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(u => u.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Unique-when-active: filtered indexes let a soft-deleted user free up their email/phone/nationalId.
        b.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(u => u.NationalId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(u => new { u.CountryCode, u.PhoneNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
