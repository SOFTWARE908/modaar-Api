using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> b)
    {
        b.ToTable("Properties");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();

        b.Property(x => x.AddressLine).HasMaxLength(500);
        b.Property(x => x.District).HasMaxLength(150);
        b.Property(x => x.City).HasMaxLength(150);

        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);

        b.Property(x => x.AnnualRent).HasPrecision(18, 2);

        b.Property(x => x.CoverImageUrl).HasMaxLength(500);

        b.Property(x => x.PropertyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // "My properties" is always scoped to one owner, so cover that read directly.
        b.HasIndex(x => new { x.OwnerUserId, x.IsDeleted });
        b.HasIndex(x => x.BrokerId);

        // NoAction, not Cascade: users are soft-deleted, and a property outliving a deactivated
        // owner account is the behaviour we want anyway.
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Dropping a brokerage unassigns its properties rather than destroying them.
        b.HasOne<Broker>()
            .WithMany()
            .HasForeignKey(x => x.BrokerId)
            .OnDelete(DeleteBehavior.SetNull);

        b.OwnsMany(x => x.Images, i =>
        {
            i.ToJson();
            i.Property(p => p.Url).IsRequired();
        });
    }
}

public class PropertyHandoverConfiguration : IEntityTypeConfiguration<PropertyHandover>
{
    public void Configure(EntityTypeBuilder<PropertyHandover> b)
    {
        b.ToTable("PropertyHandovers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.SignatureUrl).HasMaxLength(500);

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.PropertyId, x.CreatedAt });

        // At most one handover per tenancy. Filtered so the drafts that predate a contract
        // being attached don't collide on NULL.
        b.HasIndex(x => x.ContractId)
            .IsUnique()
            .HasFilter("[ContractId] IS NOT NULL");

        b.HasOne<Property>()
            .WithMany()
            .HasForeignKey(x => x.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.OwnsMany(x => x.Images, i =>
        {
            i.ToJson();
            i.Property(p => p.Url).IsRequired();
        });
    }
}
