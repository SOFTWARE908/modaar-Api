using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence.Configurations;

public class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> b)
    {
        b.ToTable("MaintenanceRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.RequestNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.ProblemDescription).HasMaxLength(2000).IsRequired();
        b.Property(x => x.RejectionReason).HasMaxLength(1000);

        b.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PreferredTimeSlot).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ScheduledTimeSlot).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RejectedBy).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => x.RequestNumber).IsUnique();

        // The three list views: tenant's own, owner/broker by property, technician's queue.
        b.HasIndex(x => new { x.TenantUserId, x.CreatedAt });
        b.HasIndex(x => new { x.PropertyId, x.Status });
        b.HasIndex(x => new { x.AssignedTechnicianId, x.Status });

        b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TenantUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Technician>().WithMany().HasForeignKey(x => x.AssignedTechnicianId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MaintenanceAttachmentConfiguration : IEntityTypeConfiguration<MaintenanceAttachment>
{
    public void Configure(EntityTypeBuilder<MaintenanceAttachment> b)
    {
        b.ToTable("MaintenanceAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => x.RequestId);

        // Finds uploads that were never attached to a request, for cleanup.
        b.HasIndex(x => new { x.UploadedByUserId, x.CreatedAt })
            .HasFilter("[RequestId] IS NULL");

        b.HasOne<MaintenanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class MaintenanceRequestActionConfiguration : IEntityTypeConfiguration<MaintenanceRequestAction>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequestAction> b)
    {
        b.ToTable("MaintenanceRequestActions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Message).HasMaxLength(2000);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.RequestId, x.CreatedAt });

        b.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Broker>().WithMany().HasForeignKey(x => x.DelegatedToBrokerId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class MaintenanceRatingConfiguration : IEntityTypeConfiguration<MaintenanceRating>
{
    public void Configure(EntityTypeBuilder<MaintenanceRating> b)
    {
        b.ToTable("MaintenanceRatings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.TipAmount).HasPrecision(18, 2);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.ToTable(t => t.HasCheckConstraint("CK_MaintenanceRatings_Stars", "[Stars] BETWEEN 1 AND 5"));

        // One rating per request.
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => x.TechnicianId);

        b.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Technician>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.NoAction);

        b.PrimitiveCollection(x => x.TagKeys).HasMaxLength(1000);
    }
}

public class MaintenanceNeedHelpConfiguration : IEntityTypeConfiguration<MaintenanceNeedHelp>
{
    public void Configure(EntityTypeBuilder<MaintenanceNeedHelp> b)
    {
        b.ToTable("MaintenanceNeedHelpTickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.IssueType).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.ContactMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => new { x.RequestId, x.CreatedAt });

        // One open ticket per request, so tapping the button twice doesn't queue duplicates.
        b.HasIndex(x => x.RequestId).IsUnique().HasFilter("[Status] = 'Open'");

        b.HasOne<MaintenanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RaisedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> b)
    {
        b.ToTable("Technicians");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.RoleAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.RoleEn).HasMaxLength(100).IsRequired();
        b.Property(x => x.RatingAverage).HasPrecision(3, 2);

        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.BrokerId);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Broker>().WithMany().HasForeignKey(x => x.BrokerId).OnDelete(DeleteBehavior.SetNull);

        b.PrimitiveCollection(x => x.Skills).HasMaxLength(200);
    }
}
