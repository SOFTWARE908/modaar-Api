using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuildingRuleCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rules were written under per-building sections, which are gone; they move to the shared
            // categories, so the few written so far are dropped rather than guessed into one.
            migrationBuilder.Sql("DELETE FROM BuildingRules;");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingRules_BuildingRuleSections_SectionId",
                table: "BuildingRules");

            migrationBuilder.DropTable(
                name: "BuildingRuleSections");

            migrationBuilder.DropIndex(
                name: "IX_BuildingRules_SectionId_SortOrder",
                table: "BuildingRules");

            migrationBuilder.RenameColumn(
                name: "SectionId",
                table: "BuildingRules",
                newName: "CategoryId");

            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "BuildingRules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "BuildingContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingContacts_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildingRuleCategories",
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
                    table.PrimaryKey("PK_BuildingRuleCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BuildingRuleCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "IconUrl", "IsActive", "NameAr", "NameEn", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000001"), "quiet_hours", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/moon.svg", true, "أوقات الهدوء", "Quiet hours", 10, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000002"), "waste_recycling", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/trash-2.svg", true, "النفايات وإعادة التدوير", "Waste and recycling", 20, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000003"), "parking", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/car.svg", true, "المواقف", "Parking", 30, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000004"), "shared_facilities", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/dumbbell.svg", true, "المرافق المشتركة", "Shared facilities", 40, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000005"), "visitors", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/users.svg", true, "الزوار", "Visitors", 50, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000006"), "safety", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/shield.svg", true, "السلامة", "Safety", 60, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b7d2e8b0-0001-4d2b-8b02-000000000007"), "general", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://cdn.jsdelivr.net/npm/lucide-static@0.460.0/icons/clipboard-list.svg", true, "عام", "General", 100, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRules_BuildingId_CategoryId_SortOrder",
                table: "BuildingRules",
                columns: new[] { "BuildingId", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRules_CategoryId",
                table: "BuildingRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingContacts_BuildingId_SortOrder",
                table: "BuildingContacts",
                columns: new[] { "BuildingId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRuleCategories_Code",
                table: "BuildingRuleCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRuleCategories_IsActive_SortOrder",
                table: "BuildingRuleCategories",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingRules_BuildingRuleCategories_CategoryId",
                table: "BuildingRules",
                column: "CategoryId",
                principalTable: "BuildingRuleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingRules_Buildings_BuildingId",
                table: "BuildingRules",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingRules_BuildingRuleCategories_CategoryId",
                table: "BuildingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingRules_Buildings_BuildingId",
                table: "BuildingRules");

            migrationBuilder.DropTable(
                name: "BuildingContacts");

            migrationBuilder.DropTable(
                name: "BuildingRuleCategories");

            migrationBuilder.DropIndex(
                name: "IX_BuildingRules_BuildingId_CategoryId_SortOrder",
                table: "BuildingRules");

            migrationBuilder.DropIndex(
                name: "IX_BuildingRules_CategoryId",
                table: "BuildingRules");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "BuildingRules");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "BuildingRules",
                newName: "SectionId");

            migrationBuilder.CreateTable(
                name: "BuildingRuleSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingRuleSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingRuleSections_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRules_SectionId_SortOrder",
                table: "BuildingRules",
                columns: new[] { "SectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingRuleSections_BuildingId_SortOrder",
                table: "BuildingRuleSections",
                columns: new[] { "BuildingId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingRules_BuildingRuleSections_SectionId",
                table: "BuildingRules",
                column: "SectionId",
                principalTable: "BuildingRuleSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
