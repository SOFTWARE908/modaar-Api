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
        b.Property(x => x.ReceiptNumber).HasMaxLength(40);
        b.Property(x => x.TransactionId).HasMaxLength(100);
        b.Property(x => x.SadadBillNumber).HasMaxLength(30);

        b.Property(p => p.DueDate);
        b.Property(p => p.PeriodStart);
        b.Property(p => p.PeriodEnd);

        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.ServiceFee).HasPrecision(18, 2);
        b.Property(x => x.VatAmount).HasPrecision(18, 2);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // The three list views: a tenant's own history, an owner's or broker's by property.
        b.HasIndex(x => new { x.PayerUserId, x.Date });
        b.HasIndex(x => new { x.PropertyId, x.Date });
        b.HasIndex(x => x.ContractId);
        b.HasIndex(p => new { p.ContractId, p.DueDate });
        // NoAction throughout: SQL Server rejects multiple cascade paths into this table, and a
        // payment must outlive a delisted property or a soft-deleted account regardless — it is
        // a financial record.
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(x => x.MaintenanceRequestId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.GatewayPaymentId).HasMaxLength(100);
        b.Property(x => x.CardBrand).HasMaxLength(20);
        b.Property(x => x.CardLast4).HasMaxLength(4);
        b.Property(x => x.FailureReason).HasMaxLength(300);
        b.Property(x => x.ReceiptUrl).HasMaxLength(500);
        b.Property(x => x.ReceiptFileName).HasMaxLength(200);
        b.Property(x => x.RejectionReason).HasMaxLength(500);

        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.PaymentId);

        // One gateway charge settles one installment, never two.
        b.HasIndex(x => x.GatewayPaymentId).IsUnique().HasFilter("[GatewayPaymentId] IS NOT NULL");
    }
}

public class ReceiptShareLinkConfiguration : IEntityTypeConfiguration<ReceiptShareLink>
{
    public void Configure(EntityTypeBuilder<ReceiptShareLink> b)
    {
        b.ToTable("ReceiptShareLinks");
        b.HasKey(x => x.Token);
        b.Property(x => x.Token).HasMaxLength(64);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.PaymentId, x.ExpiresAt });
    }
}
