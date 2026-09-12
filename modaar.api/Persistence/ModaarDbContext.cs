using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;
using Property = modaar.api.Features.Properties.Entities.Property;

namespace modaar.api.Persistence;

public class ModaarDbContext : DbContext
{
    public ModaarDbContext(DbContextOptions<ModaarDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    // Brokers
    public DbSet<Broker> Brokers => Set<Broker>();
    public DbSet<BrokerReview> BrokerReviews => Set<BrokerReview>();

    // Properties
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyHandover> PropertyHandovers => Set<PropertyHandover>();

    // Contracts
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractRequest> ContractRequests => Set<ContractRequest>();

    // Maintenance
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceAttachment> MaintenanceAttachments => Set<MaintenanceAttachment>();
    public DbSet<MaintenanceRequestAction> MaintenanceRequestActions => Set<MaintenanceRequestAction>();
    public DbSet<MaintenanceRating> MaintenanceRatings => Set<MaintenanceRating>();
    public DbSet<MaintenanceNeedHelp> MaintenanceNeedHelpTickets => Set<MaintenanceNeedHelp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModaarDbContext).Assembly);
    }
}
