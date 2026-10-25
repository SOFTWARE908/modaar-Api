using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanPerBuildingOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildingOwnerSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoApproveLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingOwnerSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingOwnerSubscriptions_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BuildingOwnerSubscriptions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingOwnerSubscriptions_BuildingId_OwnerUserId",
                table: "BuildingOwnerSubscriptions",
                columns: new[] { "BuildingId", "OwnerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingOwnerSubscriptions_OwnerUserId",
                table: "BuildingOwnerSubscriptions",
                column: "OwnerUserId");

            // Each owner's current plan and limit carry over to every building they have units in,
            // so nothing changes for anyone on deploy. Then the per-owner columns go.
            migrationBuilder.Sql(@"
INSERT INTO BuildingOwnerSubscriptions (Id, BuildingId, OwnerUserId, [Plan], AutoApproveLimit, CreatedAt, UpdatedAt)
SELECT NEWID(), pairs.BuildingId, pairs.OwnerUserId, u.SubscriptionPlan, u.AutoApproveLimit, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM (
    SELECT DISTINCT p.BuildingId, p.OwnerUserId
    FROM Properties p
    WHERE p.BuildingId IS NOT NULL AND p.IsDeleted = 0
) pairs
JOIN Users u ON u.Id = pairs.OwnerUserId;
");

            migrationBuilder.DropColumn(
                name: "AutoApproveLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AutoApproveLimit",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Managed");

            // Back to one plan per owner: the most recently changed building wins.
            migrationBuilder.Sql(@"
UPDATE u SET u.SubscriptionPlan = s.[Plan], u.AutoApproveLimit = s.AutoApproveLimit
FROM Users u
CROSS APPLY (
    SELECT TOP 1 [Plan], AutoApproveLimit FROM BuildingOwnerSubscriptions
    WHERE OwnerUserId = u.Id ORDER BY UpdatedAt DESC
) s;
");

            migrationBuilder.DropTable(
                name: "BuildingOwnerSubscriptions");
        }
    }
}
