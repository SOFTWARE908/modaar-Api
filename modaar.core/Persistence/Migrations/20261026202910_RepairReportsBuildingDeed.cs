using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairReportsBuildingDeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepairReport",
                table: "MaintenanceRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepairReportNumber",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepairReportedByUserId",
                table: "MaintenanceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TenantConfirmedAt",
                table: "MaintenanceRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "MaintenanceAttachments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Request");

            migrationBuilder.AddColumn<string>(
                name: "DeedNumber",
                table: "Buildings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearBuilt",
                table: "Buildings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_RepairReportNumber",
                table: "MaintenanceRequests",
                column: "RepairReportNumber",
                unique: true,
                filter: "[RepairReportNumber] IS NOT NULL");

            // Requests completed before confirmation existed count as confirmed, so their tenants
            // can still rate them.
            migrationBuilder.Sql(@"
UPDATE MaintenanceRequests
SET TenantConfirmedAt = COALESCE(CompletedAt, UpdatedAt)
WHERE Status = 'Completed' AND TenantConfirmedAt IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_RepairReportNumber",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "RepairReport",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "RepairReportNumber",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "RepairReportedByUserId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "TenantConfirmedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "MaintenanceAttachments");

            migrationBuilder.DropColumn(
                name: "DeedNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "YearBuilt",
                table: "Buildings");
        }
    }
}
