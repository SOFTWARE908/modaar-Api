using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceDesignFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceServiceTypes_Technicians_TechnicianId",
                table: "MaintenanceServiceTypes");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceServiceTypes_TechnicianId",
                table: "MaintenanceServiceTypes");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "MaintenanceServiceTypes");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PreferredVisitDate",
                table: "MaintenanceRequests",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "PreferredTimeSlot",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "MaintenanceRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteCostBearer",
                table: "MaintenanceRequests",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteDescription",
                table: "MaintenanceRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuoteLaborCost",
                table: "MaintenanceRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotePartsCost",
                table: "MaintenanceRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuoteRequired",
                table: "MaintenanceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuotedAt",
                table: "MaintenanceRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "MaintenanceRequests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BrokerCommunicationRating",
                table: "MaintenanceRatings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairQualityRating",
                table: "MaintenanceRatings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseSpeedRating",
                table: "MaintenanceRatings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicianRating",
                table: "MaintenanceRatings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceRatingTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRatingTags", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRatings_BrokerCommunicationRating",
                table: "MaintenanceRatings",
                sql: "[BrokerCommunicationRating] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRatings_RepairQualityRating",
                table: "MaintenanceRatings",
                sql: "[RepairQualityRating] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRatings_ResponseSpeedRating",
                table: "MaintenanceRatings",
                sql: "[ResponseSpeedRating] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRatings_TechnicianRating",
                table: "MaintenanceRatings",
                sql: "[TechnicianRating] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRatingTags_IsActive_SortOrder",
                table: "MaintenanceRatingTags",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRatingTags_Key",
                table: "MaintenanceRatingTags",
                column: "Key",
                unique: true);

            // The six chips the app shipped with. The keys are the ones existing ratings already
            // store, so those ratings keep resolving.
            migrationBuilder.Sql("""
                INSERT INTO MaintenanceRatingTags (Id, [Key], LabelAr, LabelEn, SortOrder, IsActive, CreatedAt, UpdatedAt)
                VALUES
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A01', 'maintenance_rating.tag_comfortable',        N'تعامل مريح',  N'Easy to deal with',  10, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A02', 'maintenance_rating.tag_great_personality',  N'شخصية رائعة', N'Great personality',  20, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A03', 'maintenance_rating.tag_clean_comfortable',  N'نظيف ومرتّب', N'Clean and tidy',     30, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A04', 'maintenance_rating.tag_good_communication', N'تواصل جيد',   N'Good communication', 40, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A05', 'maintenance_rating.tag_excellent_service',  N'خدمة ممتازة', N'Excellent service',  50, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
                    ('7C1E9A40-2B6D-4F18-8E3A-5D0C2F9B1A06', 'maintenance_rating.tag_respectful',         N'محترم',       N'Respectful',         60, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                """);

            // The design's categories: plumbing, electrical, AC, carpentry, other — in that order.
            // The original three stay active (existing requests point at them) but sort after;
            // deactivate them from the admin endpoint if they should leave the picker.
            migrationBuilder.Sql("""
                INSERT INTO MaintenanceServiceTypes
                    (Id, Code, NameAr, NameEn, IconUrl, SortOrder, IsActive, CreatedAt, UpdatedAt)
                SELECT v.Id, v.Code, v.NameAr, v.NameEn, NULL, v.SortOrder, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                  FROM (VALUES
                        ('5B0E2C1D-7A3F-4E61-9C2B-1F8D4A6E3B01', 'plumbing',   N'سباكة',  N'Plumbing',   10),
                        ('5B0E2C1D-7A3F-4E61-9C2B-1F8D4A6E3B02', 'electrical', N'كهرباء', N'Electrical', 20),
                        ('5B0E2C1D-7A3F-4E61-9C2B-1F8D4A6E3B03', 'carpentry',  N'نجارة',  N'Carpentry',  40),
                        ('5B0E2C1D-7A3F-4E61-9C2B-1F8D4A6E3B04', 'other',      N'أخرى',   N'Other',      90)
                       ) AS v (Id, Code, NameAr, NameEn, SortOrder)
                 WHERE NOT EXISTS (SELECT 1 FROM MaintenanceServiceTypes t WHERE t.Code = v.Code);

                UPDATE MaintenanceServiceTypes
                   SET SortOrder = CASE Code
                                       WHEN 'ac'          THEN 30
                                       WHEN 'cleaning'    THEN 50
                                       WHEN 'paintRepair' THEN 60
                                       WHEN 'appliances'  THEN 70
                                   END,
                       UpdatedAt = SYSDATETIMEOFFSET()
                 WHERE Code IN ('ac', 'cleaning', 'paintRepair', 'appliances');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only categories nothing was filed under; a used one has to stay for its requests.
            migrationBuilder.Sql("""
                DELETE t
                  FROM MaintenanceServiceTypes t
                 WHERE t.Code IN ('plumbing', 'electrical', 'carpentry', 'other')
                   AND NOT EXISTS (SELECT 1 FROM MaintenanceRequests r WHERE r.ServiceTypeId = t.Id)
                   AND NOT EXISTS (SELECT 1 FROM TechnicianServiceTypes s WHERE s.ServiceTypeId = t.Id);
                """);

            migrationBuilder.DropTable(
                name: "MaintenanceRatingTags");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRatings_BrokerCommunicationRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRatings_RepairQualityRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRatings_ResponseSpeedRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRatings_TechnicianRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteCostBearer",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteDescription",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteLaborCost",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuotePartsCost",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuoteRequired",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "QuotedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "BrokerCommunicationRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropColumn(
                name: "RepairQualityRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropColumn(
                name: "ResponseSpeedRating",
                table: "MaintenanceRatings");

            migrationBuilder.DropColumn(
                name: "TechnicianRating",
                table: "MaintenanceRatings");

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "MaintenanceServiceTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PreferredVisitDate",
                table: "MaintenanceRequests",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreferredTimeSlot",
                table: "MaintenanceRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServiceTypes_TechnicianId",
                table: "MaintenanceServiceTypes",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceServiceTypes_Technicians_TechnicianId",
                table: "MaintenanceServiceTypes",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }
    }
}
