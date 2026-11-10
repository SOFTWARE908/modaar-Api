using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Wallets.Entities;

namespace modaar.api.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.ToTable("Wallets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Balance).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // One wallet per user; also what stops two first requests creating two.
        b.HasIndex(x => x.UserId).IsUnique();
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> b)
    {
        b.ToTable("WalletTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.GatewayPaymentId).HasMaxLength(100);
        b.Property(x => x.CardBrand).HasMaxLength(20);
        b.Property(x => x.CardLast4).HasMaxLength(4);
        b.Property(x => x.FailureReason).HasMaxLength(300);

        b.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.WalletId, x.CreatedAt });

        // One gateway charge credits a wallet once.
        b.HasIndex(x => x.GatewayPaymentId).IsUnique().HasFilter("[GatewayPaymentId] IS NOT NULL");
    }
}
