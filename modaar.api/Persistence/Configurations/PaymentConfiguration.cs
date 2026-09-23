using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PaymentMethod).HasMaxLength(50);
        b.Property(x => x.InvoiceUrl).HasMaxLength(500);

        b.Property(x => x.Amount).HasPrecision(18, 2);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // The three list views: a tenant's own history, an owner's or broker's by property.
        b.HasIndex(x => new { x.PayerUserId, x.Date });
        b.HasIndex(x => new { x.PropertyId, x.Date });
        b.HasIndex(x => x.ContractId);

        // NoAction throughout: SQL Server rejects multiple cascade paths into this table, and a
        // payment must outlive a delisted property or a soft-deleted account regardless — it is
        // a financial record.
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(x => x.MaintenanceRequestId).OnDelete(DeleteBehavior.NoAction);
    }
}
