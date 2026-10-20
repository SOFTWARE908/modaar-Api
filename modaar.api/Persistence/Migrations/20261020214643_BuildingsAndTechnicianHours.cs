using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuildingsAndTechnicianHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "Properties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Buildings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianWorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Start = table.Column<TimeOnly>(type: "time", nullable: false),
                    End = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianWorkingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianWorkingHours_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_BuildingId",
                table: "Properties",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_CreatedByUserId",
                table: "Buildings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianWorkingHours_TechnicianId_DayOfWeek",
                table: "TechnicianWorkingHours",
                columns: new[] { "TechnicianId", "DayOfWeek" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Buildings_BuildingId",
                table: "Properties",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Every live unit into a building. Units with the same name in the same city and
            // district are one building, whoever owns them — a building can hold several owners'
            // units. A unit with a unique name (a villa) becomes a building of one. The building is
            // credited to one of the owners as its creator, which only decides who may rename it.
            migrationBuilder.Sql(@"
INSERT INTO Buildings (Id, Name, Type, AddressLine, District, City, Latitude, Longitude, CoverImageUrl, CreatedByUserId, CreatedAt, UpdatedAt)
SELECT NEWID(), g.Name, g.Type, g.AddressLine, NULLIF(g.District, ''), NULLIF(g.City, ''), g.Lat, g.Lng, g.Cover, g.CreatedBy,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM (
    SELECT LTRIM(RTRIM(Title)) AS Name,
           ISNULL(City, '') AS City,
           ISNULL(District, '') AS District,
           MIN(AddressLine) AS AddressLine,
           MIN(Latitude) AS Lat,
           MIN(Longitude) AS Lng,
           MIN(CoverImageUrl) AS Cover,
           CASE WHEN SUM(CASE WHEN PropertyType = 'Villa' THEN 1 ELSE 0 END) = COUNT(*)
                THEN 'VillaCompound' ELSE 'ResidentialBuilding' END AS Type,
           CAST(MIN(CAST(OwnerUserId AS char(36))) AS uniqueidentifier) AS CreatedBy
    FROM Properties
    WHERE IsDeleted = 0
    GROUP BY LTRIM(RTRIM(Title)), ISNULL(City, ''), ISNULL(District, '')
) g;");

            migrationBuilder.Sql(@"
UPDATE p SET p.BuildingId = b.Id
FROM Properties p
JOIN Buildings b ON b.Name = LTRIM(RTRIM(p.Title))
                AND ISNULL(b.City, '') = ISNULL(p.City, '')
                AND ISNULL(b.District, '') = ISNULL(p.District, '')
WHERE p.IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Buildings_BuildingId",
                table: "Properties");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "TechnicianWorkingHours");

            migrationBuilder.DropIndex(
                name: "IX_Properties_BuildingId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "Properties");
        }
    }
}
