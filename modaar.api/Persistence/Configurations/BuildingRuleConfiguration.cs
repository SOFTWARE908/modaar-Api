using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using modaar.api.Features.Buildings.Entities;

namespace modaar.api.Persistence.Configurations;

public class BuildingRuleSectionConfiguration : IEntityTypeConfiguration<BuildingRuleSection>
{
    public void Configure(EntityTypeBuilder<BuildingRuleSection> b)
    {
        b.ToTable("BuildingRuleSections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.BuildingId, x.SortOrder });

        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Rules).WithOne().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BuildingRuleConfiguration : IEntityTypeConfiguration<BuildingRule>
{
    public void Configure(EntityTypeBuilder<BuildingRule> b)
    {
        b.ToTable("BuildingRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Text).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.SectionId, x.SortOrder });
    }
}
