using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminBuildingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcType",
                table: "Properties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcUnitsCount",
                table: "Properties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElectricityMeterNumber",
                table: "Properties",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasKitchenCabinets",
                table: "Properties",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalNumber",
                table: "Buildings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingNumber",
                table: "Buildings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Buildings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeedIssueDateHijri",
                table: "Buildings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeedType",
                table: "Buildings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloorsCount",
                table: "Buildings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingSpaces",
                table: "Buildings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Buildings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Buildings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsCount",
                table: "Buildings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsagePurpose",
                table: "Buildings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Cities",
                columns: new[] { "Id", "Code", "IsActive", "NameAr", "NameEn", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("c1700000-0000-4000-8000-000000000001"), "riyadh", true, "الرياض", "Riyadh", 10 },
                    { new Guid("c1700000-0000-4000-8000-000000000002"), "makkah", true, "مكة المكرمة", "Makkah", 20 },
                    { new Guid("c1700000-0000-4000-8000-000000000003"), "madinah", true, "المدينة المنورة", "Madinah", 30 },
                    { new Guid("c1700000-0000-4000-8000-000000000004"), "buraydah", true, "بريدة", "Buraydah", 40 },
                    { new Guid("c1700000-0000-4000-8000-000000000005"), "dammam", true, "الدمام", "Dammam", 50 },
                    { new Guid("c1700000-0000-4000-8000-000000000006"), "tabuk", true, "تبوك", "Tabuk", 60 },
                    { new Guid("c1700000-0000-4000-8000-000000000007"), "hail", true, "حائل", "Hail", 70 },
                    { new Guid("c1700000-0000-4000-8000-000000000008"), "arar", true, "عرعر", "Arar", 80 },
                    { new Guid("c1700000-0000-4000-8000-000000000009"), "sakaka", true, "سكاكا", "Sakaka", 90 },
                    { new Guid("c1700000-0000-4000-8000-000000000010"), "abha", true, "أبها", "Abha", 100 },
                    { new Guid("c1700000-0000-4000-8000-000000000011"), "jazan", true, "جازان", "Jazan", 110 },
                    { new Guid("c1700000-0000-4000-8000-000000000012"), "najran", true, "نجران", "Najran", 120 },
                    { new Guid("c1700000-0000-4000-8000-000000000013"), "al_bahah", true, "الباحة", "Al Bahah", 130 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_CityId",
                table: "Buildings",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Code",
                table: "Cities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IsActive_SortOrder",
                table: "Cities",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Cities_CityId",
                table: "Buildings",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Buildings already typed with one of these city names get linked to it. City text stays.
            migrationBuilder.Sql(@"
UPDATE b SET b.CityId = c.Id
FROM Buildings b
JOIN Cities c ON LTRIM(RTRIM(b.City)) = c.NameAr
WHERE b.CityId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Cities_CityId",
                table: "Buildings");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_CityId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AcType",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "AcUnitsCount",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ElectricityMeterNumber",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasKitchenCabinets",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "AdditionalNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "BuildingNumber",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DeedIssueDateHijri",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "DeedType",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "FloorsCount",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "ParkingSpaces",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "UnitsCount",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "UsagePurpose",
                table: "Buildings");
        }
    }
}
