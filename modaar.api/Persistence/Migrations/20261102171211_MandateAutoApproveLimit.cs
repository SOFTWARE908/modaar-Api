using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MandateAutoApproveLimit : Migration
    {
        /// <inheritdoc />
        // Mandates are now sent by the owner (units + maintenance auto-approve limit) and accepted by
        // the broker; no fees. The limit moves here from BuildingOwnerSubscriptions, and the owner's
        // requests table goes: a pending request becomes a mandate waiting for the broker.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AutoApproveLimit",
                table: "ManagementAgreements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Broker-written proposals still waiting for the owner don't fit the new flow.
            migrationBuilder.Sql("""
                UPDATE ManagementAgreements
                SET Status = 'Withdrawn',
                    EndedOn = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+03:00') AS date),
                    EndReason = 'Retired: mandates are now sent by the owner.',
                    UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Status = 'PendingOwner';

                UPDATE AgreementEvents SET Type = 'Accepted' WHERE Type = 'Approved';
                """);

            // Pending owner requests → mandates waiting for the broker, numbered after the last one
            // this year. Same id as the request. Whole-portfolio requests ('[]') take every unit.
            migrationBuilder.Sql("""
                DECLARE @prefix nvarchar(10) = CONCAT('MD-', YEAR(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+03:00')), '-');
                DECLARE @last int = ISNULL((SELECT MAX(CAST(SUBSTRING(Number, LEN(@prefix) + 1, 10) AS int))
                                            FROM ManagementAgreements WHERE Number LIKE @prefix + '%'), 0);

                WITH r AS (
                    SELECT req.*, ROW_NUMBER() OVER (ORDER BY req.CreatedAt) AS rn
                    FROM BrokerManagementRequests req
                    WHERE req.Status = 'Pending'
                      AND EXISTS (SELECT 1 FROM Properties p WHERE p.OwnerUserId = req.OwnerUserId AND p.IsDeleted = 0))
                INSERT INTO ManagementAgreements (Id, Number, BrokerId, OwnerUserId, Status, CreatedByUserId, CreatedAt, UpdatedAt)
                SELECT Id, CONCAT(@prefix, RIGHT(CONCAT('000000', @last + rn), 6)), BrokerId, OwnerUserId,
                       'PendingBroker', OwnerUserId, CreatedAt, SYSDATETIMEOFFSET()
                FROM r;

                INSERT INTO AgreementProperties (Id, AgreementId, PropertyId)
                SELECT NEWID(), a.Id, p.Id
                FROM ManagementAgreements a
                JOIN BrokerManagementRequests req ON req.Id = a.Id
                JOIN Properties p ON p.OwnerUserId = a.OwnerUserId AND p.IsDeleted = 0
                WHERE a.Status = 'PendingBroker'
                  AND (req.PropertyIds = '[]'
                       OR p.Id IN (SELECT TRY_CAST([value] AS uniqueidentifier) FROM OPENJSON(req.PropertyIds)));

                INSERT INTO AgreementEvents (Id, AgreementId, Type, ActorUserId, At)
                SELECT NEWID(), a.Id, 'Created', a.OwnerUserId, a.CreatedAt
                FROM ManagementAgreements a
                JOIN BrokerManagementRequests req ON req.Id = a.Id;
                """);

            // The limit the broker had set per building, onto the mandate covering those units: the
            // lowest of them, the safer one for the owner. Active → the units it holds now.
            migrationBuilder.Sql("""
                UPDATE a
                SET AutoApproveLimit = (
                    SELECT MIN(s.AutoApproveLimit)
                    FROM AgreementProperties ap
                    JOIN Properties p ON p.Id = ap.PropertyId
                    JOIN BuildingOwnerSubscriptions s ON s.BuildingId = p.BuildingId AND s.OwnerUserId = a.OwnerUserId
                    WHERE ap.AgreementId = a.Id
                      AND s.AutoApproveLimit IS NOT NULL
                      AND (a.Status = 'PendingBroker' OR (ap.ActiveFrom IS NOT NULL AND ap.ExitedOn IS NULL)))
                FROM ManagementAgreements a
                WHERE a.Status IN ('Active', 'PendingBroker');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ManagementAgreements_BrokerManagementRequests_RequestId",
                table: "ManagementAgreements");

            migrationBuilder.DropTable(
                name: "BrokerManagementRequests");

            migrationBuilder.DropIndex(
                name: "IX_ManagementAgreements_RequestId",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "LeasingFeeType",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "ManagementFeePercent",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "LeasingFeeValue",
                table: "ManagementAgreements");

            migrationBuilder.DropColumn(
                name: "AutoApproveLimit",
                table: "BuildingOwnerSubscriptions");
        }

        /// <inheritdoc />
        // Structure only: limits, fees and the requests table come back empty. Mandates waiting for
        // a broker have no place in the old flow and are withdrawn.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE ManagementAgreements
                SET Status = 'Withdrawn', EndedOn = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+03:00') AS date),
                    EndReason = 'Rolled back.', UpdatedAt = SYSDATETIMEOFFSET()
                WHERE Status = 'PendingBroker';

                UPDATE AgreementEvents SET Type = 'Approved' WHERE Type = 'Accepted';
                """);

            migrationBuilder.DropColumn(
                name: "AutoApproveLimit",
                table: "ManagementAgreements");

            migrationBuilder.AddColumn<decimal>(
                name: "LeasingFeeValue",
                table: "ManagementAgreements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                table: "ManagementAgreements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeasingFeeType",
                table: "ManagementAgreements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManagementFeePercent",
                table: "ManagementAgreements",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "ManagementAgreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AutoApproveLimit",
                table: "BuildingOwnerSubscriptions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BrokerManagementRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerManagementRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrokerManagementRequests_Brokers_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Brokers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BrokerManagementRequests_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_RequestId",
                table: "ManagementAgreements",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerManagementRequests_BrokerId_Status",
                table: "BrokerManagementRequests",
                columns: new[] { "BrokerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerManagementRequests_OwnerUserId_BrokerId",
                table: "BrokerManagementRequests",
                columns: new[] { "OwnerUserId", "BrokerId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerManagementRequests_OwnerUserId_CreatedAt",
                table: "BrokerManagementRequests",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ManagementAgreements_BrokerManagementRequests_RequestId",
                table: "ManagementAgreements",
                column: "RequestId",
                principalTable: "BrokerManagementRequests",
                principalColumn: "Id");
        }
    }
}
