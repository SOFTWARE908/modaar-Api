using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Authentication.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Persistence;

public class ModaarDbContext : DbContext
{
    public ModaarDbContext(DbContextOptions<ModaarDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModaarDbContext).Assembly);
    }
}
