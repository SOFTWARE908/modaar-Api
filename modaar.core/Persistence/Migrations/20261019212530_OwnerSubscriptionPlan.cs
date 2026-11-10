using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuoteApprovedAt",
                table: "MaintenanceRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteApprovedByUserId",
                table: "MaintenanceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuoteAutoApproved",
                table: "MaintenanceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduledVisitTime",
                table: "MaintenanceRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Managed");

            // Existing requests on a property with no broker were always run by the owner.
            migrationBuilder.Sql(@"
UPDATE m SET m.SubscriptionPlan = 'SelfManaged'
FROM MaintenanceRequests m
JOIN Properties p ON p.Id = m.PropertyId
WHERE p.BrokerId IS NULL;");

            // Quotes from before owner approval existed were never waiting on anyone; treat them as
            // approved so those requests don't suddenly stall at awaitingOwnerApproval.
            migrationBuilder.Sql(@"
UPDATE MaintenanceRequests SET QuoteApprovedAt = QuotedAt
WHERE QuotedAt IS NOT NULL AND QuoteApprovedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoApproveLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "QuoteApprovedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteApprovedByUserId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteAutoApproved",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ScheduledVisitTime",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "MaintenanceRequests");
        }
    }
}
