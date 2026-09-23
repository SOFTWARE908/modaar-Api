using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Payments.Entities;

namespace modaar.api.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> b)
    {
        b.ToTable("InvoiceItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Label).HasMaxLength(200).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);

        b.HasIndex(x => new { x.PaymentId, x.SortOrder });

        // The only cascade in the payments slice: a line has no meaning without its invoice.
        b.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
