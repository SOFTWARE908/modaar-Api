using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.ToTable("Contracts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.TerminationReason).HasMaxLength(1000);
        b.Property(x => x.PdfUrl).HasMaxLength(500);

        b.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => x.ContractNumber).IsUnique();

        // One live lease per unit — the physical rule, enforced in the schema rather than in a
        // service that can be bypassed. A property's whole history stays queryable alongside it.
        b.HasIndex(x => x.PropertyId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'");

        // A tenant may hold several active leases (two units), so no equivalent index here.
        b.HasIndex(x => new { x.TenantUserId, x.Status });
        b.HasIndex(x => x.EndDate);

        b.ToTable(t => t.HasCheckConstraint("CK_Contracts_Dates", "[EndDate] > [StartDate]"));

        // NoAction throughout: SQL Server rejects multiple cascade paths into this table, and a
        // lease should outlive a soft-deleted account or a delisted property regardless.
        b.HasOne<Property>()
            .WithMany()
            .HasForeignKey(x => x.PropertyId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TenantUserId)
            .OnDelete(DeleteBehavior.NoAction);

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

public class ContractRequestConfiguration : IEntityTypeConfiguration<ContractRequest>
{
    public void Configure(EntityTypeBuilder<ContractRequest> b)
    {
        b.ToTable("ContractRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.DecisionReason).HasMaxLength(1000);

        b.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // History is listed newest-first for one contract.
        b.HasIndex(x => new { x.ContractId, x.CreatedAt });

        // One open request of a given kind per contract: stops a tenant queueing five renewals
        // by tapping the button repeatedly.
        b.HasIndex(x => new { x.ContractId, x.Type })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        b.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
