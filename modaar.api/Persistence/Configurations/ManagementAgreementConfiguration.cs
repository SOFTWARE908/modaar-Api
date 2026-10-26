using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Agreements.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

// NoAction throughout: Users already cascade into Brokers and Properties, and a second path into
// these tables is what SQL Server rejects.
public class ManagementAgreementConfiguration : IEntityTypeConfiguration<ManagementAgreement>
{
    public void Configure(EntityTypeBuilder<ManagementAgreement> b)
    {
        b.ToTable("ManagementAgreements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ManagementFeePercent).HasPrecision(5, 2);
        b.Property(x => x.LeasingFeeType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LeasingFeeValue).HasPrecision(18, 2);
        b.Property(x => x.DocumentUrl).HasMaxLength(500);
        b.Property(x => x.EndReason).HasMaxLength(1000);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.BrokerId, x.Status });
        b.HasIndex(x => new { x.OwnerUserId, x.Status });

        b.HasOne(x => x.Broker).WithMany().HasForeignKey(x => x.BrokerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<BrokerManagementRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ManagementAgreement>().WithMany().HasForeignKey(x => x.RenewedFromId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Properties).WithOne().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgreementPropertyConfiguration : IEntityTypeConfiguration<AgreementProperty>
{
    public void Configure(EntityTypeBuilder<AgreementProperty> b)
    {
        b.ToTable("AgreementProperties");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.ExitReason).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(x => new { x.AgreementId, x.PropertyId }).IsUnique();

        // A unit is held by at most one agreement at a time.
        b.HasIndex(x => x.PropertyId)
            .IsUnique()
            .HasFilter("[ActiveFrom] IS NOT NULL AND [ExitedOn] IS NULL")
            .HasDatabaseName("IX_AgreementProperties_PropertyId_Held");

        b.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AgreementEventConfiguration : IEntityTypeConfiguration<AgreementEvent>
{
    public void Configure(EntityTypeBuilder<AgreementEvent> b)
    {
        b.ToTable("AgreementEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.At).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.AgreementId, x.At });

        b.HasOne<ManagementAgreement>().WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
    }
}
