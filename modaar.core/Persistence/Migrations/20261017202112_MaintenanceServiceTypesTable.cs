using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceServiceTypesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceServiceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceServiceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceServiceTypes_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceTypes_Code",
                table: "MaintenanceServiceTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceTypes_IsActive_SortOrder",
                table: "MaintenanceServiceTypes",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceTypes_TechnicianId",
                table: "MaintenanceServiceTypes",
                column: "TechnicianId");



            migrationBuilder.Sql("""
        INSERT INTO MaintenanceServiceTypes
            (Id, Code, NameAr, NameEn, IconUrl, SortOrder, IsActive, CreatedAt, UpdatedAt)
        VALUES
            ('AE58170A-18CF-4F72-A793-9DF2B1C1A93B', 'cleaning',    N'تنظيف',        N'Cleaning',         NULL, 10, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
            ('825F9294-32BF-475A-AF02-80CA7030DC4C', 'paintRepair', N'دهان وترميم',  N'Paint & repair',   NULL, 20, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
            ('16758CA8-5399-43D3-9CF7-F723241769D8', 'appliances',  N'أجهزة منزلية', N'Appliances',       NULL, 30, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
            ('2381BB99-2435-4148-80C7-3141D6AFF346', 'ac',          N'تكييف',        N'Air conditioning', NULL, 40, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
        """);
            //////////////////////


            migrationBuilder.AddColumn<Guid>(
                name: "ServiceTypeId",
                table: "MaintenanceRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));


            migrationBuilder.Sql("""
        UPDATE r
           SET r.ServiceTypeId = t.Id
          FROM MaintenanceRequests r
          JOIN MaintenanceServiceTypes t
            ON t.Code = CASE r.ServiceType
                            WHEN 'Cleaning'    THEN 'cleaning'
                            WHEN 'PaintRepair' THEN 'paintRepair'
                            WHEN 'Appliances'  THEN 'appliances'
                            WHEN 'Ac'          THEN 'ac'
                        END;
        """);


            

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "MaintenanceRequests");

            

            

            migrationBuilder.CreateTable(
                name: "TechnicianServiceTypes",
                columns: table => new
                {
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianServiceTypes", x => new { x.TechnicianId, x.ServiceTypeId });
                    table.ForeignKey(
                        name: "FK_TechnicianServiceTypes_MaintenanceServiceTypes_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "MaintenanceServiceTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicianServiceTypes_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
        INSERT INTO TechnicianServiceTypes (TechnicianId, ServiceTypeId)
        SELECT DISTINCT t.Id, st.Id
          FROM Technicians t
         CROSS APPLY OPENJSON(t.Skills) AS s
          JOIN MaintenanceServiceTypes st
            ON st.Code = CASE s.value
                             WHEN 'Cleaning'    THEN 'cleaning'
                             WHEN 'PaintRepair' THEN 'paintRepair'
                             WHEN 'Appliances'  THEN 'appliances'
                             WHEN 'Ac'          THEN 'ac'
                         END
         WHERE ISJSON(t.Skills) = 1;
        """);

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "Technicians");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ServiceTypeId_CreatedAt",
                table: "MaintenanceRequests",
                columns: new[] { "ServiceTypeId", "CreatedAt" });

            

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianServiceTypes_ServiceTypeId",
                table: "TechnicianServiceTypes",
                column: "ServiceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_MaintenanceServiceTypes_ServiceTypeId",
                table: "MaintenanceRequests",
                column: "ServiceTypeId",
                principalTable: "MaintenanceServiceTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_MaintenanceServiceTypes_ServiceTypeId",
                table: "MaintenanceRequests");

            migrationBuilder.DropTable(
                name: "TechnicianServiceTypes");

            migrationBuilder.DropTable(
                name: "MaintenanceServiceTypes");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_ServiceTypeId_CreatedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceTypeId",
                table: "MaintenanceRequests");

            migrationBuilder.AddColumn<string>(
                name: "Skills",
                table: "Technicians",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
