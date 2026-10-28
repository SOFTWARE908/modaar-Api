using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuildingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RulesUpdatedAt",
                table: "Buildings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildingRuleSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "BuildingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingRules_BuildingRuleSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "BuildingRuleSections",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingRules");

            migrationBuilder.DropTable(
                name: "BuildingRuleSections");

            migrationBuilder.DropColumn(
                name: "RulesUpdatedAt",
                table: "Buildings");
        }
    }
}
