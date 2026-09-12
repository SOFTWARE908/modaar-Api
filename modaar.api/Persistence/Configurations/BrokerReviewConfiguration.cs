using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations
{
    public class BrokerReviewConfiguration : IEntityTypeConfiguration<BrokerReview>
    {
        public void Configure(EntityTypeBuilder<BrokerReview> b)
        {
            b.ToTable("BrokerReviews");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Comment).HasMaxLength(1000);

            b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            b.ToTable(t => t.HasCheckConstraint("CK_BrokerReviews_Rating", "[Rating] BETWEEN 1 AND 5"));

            // One review per reviewer per brokerage.
            b.HasIndex(x => new { x.BrokerId, x.ReviewerUserId }).IsUnique();

            // Reviews are listed newest-first and paged, so cover that read directly.
            b.HasIndex(x => new { x.BrokerId, x.CreatedAt });

            // NoAction, not Cascade: Users already cascades into Brokers, and Brokers into here
            // would close a second path into this table that SQL Server rejects.
            b.HasOne(x => x.Broker)
                .WithMany()
                .HasForeignKey(x => x.BrokerId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Reviewer)
                .WithMany()
                .HasForeignKey(x => x.ReviewerUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
