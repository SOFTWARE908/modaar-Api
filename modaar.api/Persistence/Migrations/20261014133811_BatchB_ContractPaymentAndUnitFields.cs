using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BatchB_ContractPaymentAndUnitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MonthlyAmount",
                table: "Contracts",
                newName: "InstallmentAmount");

            migrationBuilder.AddColumn<string>(
                name: "UnitNumber",
                table: "Properties",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Payments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "Payments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "Payments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualRent",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentFrequency",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDeposit",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ContractId_DueDate",
                table: "Payments",
                columns: new[] { "ContractId", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ContractId_DueDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "AnnualRent",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentFrequency",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SecurityDeposit",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "InstallmentAmount",
                table: "Contracts",
                newName: "MonthlyAmount");
        }
    }
}
