using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BrokerMarketplace : Migration
    {
        /// <inheritdoc />
        // Marketplace: broker services become an admin-managed list (the free-text JSON list goes,
        // names that match a service carry over), years of experience, 4-criteria reviews, and
        // the owner's message on a mandate.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "ManagementAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceSinceYear",
                table: "Brokers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagementRating",
                table: "BrokerReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MarketingRating",
                table: "BrokerReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResponsivenessRating",
                table: "BrokerReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransparencyRating",
                table: "BrokerReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BrokerServiceCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerServiceCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrokerOfferedServices",
                columns: table => new
                {
                    BrokerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerOfferedServices", x => new { x.BrokerId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_BrokerOfferedServices_BrokerServiceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "BrokerServiceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrokerOfferedServices_Brokers_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Brokers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BrokerServiceCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "IconUrl", "IsActive", "NameAr", "NameEn", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("c3a1f0d2-0001-4b6e-9a01-000000000001"), "property_management", new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/building-2.svg", true, "إدارة أملاك", "Property management", 10, new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("c3a1f0d2-0001-4b6e-9a01-000000000002"), "marketing_leasing", new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/megaphone.svg", true, "تسويق وتأجير", "Marketing and leasing", 20, new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("c3a1f0d2-0001-4b6e-9a01-000000000003"), "commercial", new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/store.svg", true, "تجاري", "Commercial", 30, new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("c3a1f0d2-0001-4b6e-9a01-000000000004"), "maintenance", new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/wrench.svg", true, "صيانة", "Maintenance", 40, new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            // A broker's free-text service whose Arabic or English name matches a service's name
            // becomes that service; anything else they re-pick from the list.
            migrationBuilder.Sql("""
                INSERT INTO BrokerOfferedServices (BrokerId, CategoryId)
                SELECT DISTINCT b.Id, c.Id
                FROM Brokers b
                CROSS APPLY OPENJSON(b.Services) WITH (NameAr nvarchar(200) '$.NameAr', NameEn nvarchar(200) '$.NameEn') s
                JOIN BrokerServiceCategories c
                  ON LTRIM(RTRIM(s.NameAr)) = c.NameAr OR LTRIM(RTRIM(s.NameEn)) = c.NameEn
                WHERE b.Services IS NOT NULL AND ISJSON(b.Services) = 1;
                """);

            migrationBuilder.DropColumn(
                name: "Services",
                table: "Brokers");

            // Reviews written before the four criteria: each criterion takes the one rating given.
            migrationBuilder.Sql("""
                UPDATE BrokerReviews
                SET ResponsivenessRating = Rating, ManagementRating = Rating,
                    TransparencyRating = Rating, MarketingRating = Rating;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BrokerReviews_Criteria",
                table: "BrokerReviews",
                sql: "[ResponsivenessRating] BETWEEN 1 AND 5 AND [ManagementRating] BETWEEN 1 AND 5 AND [TransparencyRating] BETWEEN 1 AND 5 AND [MarketingRating] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerOfferedServices_CategoryId",
                table: "BrokerOfferedServices",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerServiceCategories_Code",
                table: "BrokerServiceCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerServiceCategories_IsActive_SortOrder",
                table: "BrokerServiceCategories",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerOfferedServices");

            migrationBuilder.DropTable(
                name: "BrokerServiceCategories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BrokerReviews_Criteria",
                table: "BrokerReviews");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "ExperienceSinceYear",
                table: "Brokers");

            migrationBuilder.DropColumn(
                name: "ManagementRating",
                table: "BrokerReviews");

            migrationBuilder.DropColumn(
                name: "MarketingRating",
                table: "BrokerReviews");

            migrationBuilder.DropColumn(
                name: "ResponsivenessRating",
                table: "BrokerReviews");

            migrationBuilder.DropColumn(
                name: "TransparencyRating",
                table: "BrokerReviews");

            migrationBuilder.AddColumn<string>(
                name: "Services",
                table: "Brokers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
