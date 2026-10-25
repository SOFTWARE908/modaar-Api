using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagementAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagementAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BrokerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ManagementFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LeasingFeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LeasingFeeValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RenewedFromId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementAgreements_BrokerManagementRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "BrokerManagementRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ManagementAgreements_Brokers_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Brokers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ManagementAgreements_ManagementAgreements_RenewedFromId",
                        column: x => x.RenewedFromId,
                        principalTable: "ManagementAgreements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ManagementAgreements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ManagementAgreements_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AgreementEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementEvents_ManagementAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ManagementAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementProperties_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AgreementProperties_ManagementAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "ManagementAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgreementProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementEvents_AgreementId_At",
                table: "AgreementEvents",
                columns: new[] { "AgreementId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementProperties_AgreementId_PropertyId",
                table: "AgreementProperties",
                columns: new[] { "AgreementId", "PropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgreementProperties_ContractId",
                table: "AgreementProperties",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AgreementProperties_PropertyId_Held",
                table: "AgreementProperties",
                column: "PropertyId",
                unique: true,
                filter: "[ActiveFrom] IS NOT NULL AND [ExitedOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_BrokerId_Status",
                table: "ManagementAgreements",
                columns: new[] { "BrokerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_CreatedByUserId",
                table: "ManagementAgreements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_Number",
                table: "ManagementAgreements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_OwnerUserId_Status",
                table: "ManagementAgreements",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_RenewedFromId",
                table: "ManagementAgreements",
                column: "RenewedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementAgreements_RequestId",
                table: "ManagementAgreements",
                column: "RequestId");

            // Every owner–broker link that exists today becomes an active agreement, so nothing
            // changes for anyone on deploy. No fee: those terms were never recorded. Each unit
            // leaves with its current lease, or stays until the first one if it is vacant.
            migrationBuilder.Sql(@"
;WITH pairs AS (
    SELECT p.OwnerUserId, p.BrokerId, MIN(p.CreatedAt) AS FirstAt
    FROM Properties p
    WHERE p.BrokerId IS NOT NULL AND p.IsDeleted = 0
    GROUP BY p.OwnerUserId, p.BrokerId
), numbered AS (
    SELECT pairs.*, ROW_NUMBER() OVER (ORDER BY FirstAt, OwnerUserId) AS n FROM pairs
)
INSERT INTO ManagementAgreements (Id, Number, BrokerId, OwnerUserId, Status, CreatedByUserId, StartDate, ApprovedAt, CreatedAt, UpdatedAt)
SELECT NEWID(),
       CONCAT('MD-', YEAR(SYSDATETIMEOFFSET()), '-', RIGHT('000000' + CAST(n AS varchar(6)), 6)),
       numbered.BrokerId, numbered.OwnerUserId, 'Active',
       b.UserId, CAST(numbered.FirstAt AS date), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM numbered
JOIN Brokers b ON b.Id = numbered.BrokerId;

INSERT INTO AgreementProperties (Id, AgreementId, PropertyId, ActiveFrom, ContractId, EndsOn)
SELECT NEWID(), a.Id, p.Id, a.StartDate, lease.Id, COALESCE(lease.TerminatedOn, lease.EndDate)
FROM Properties p
JOIN ManagementAgreements a ON a.OwnerUserId = p.OwnerUserId AND a.BrokerId = p.BrokerId
OUTER APPLY (
    SELECT TOP 1 c.Id, c.EndDate, c.TerminatedOn
    FROM Contracts c
    WHERE c.PropertyId = p.Id AND c.Status = 'Active'
    ORDER BY c.StartDate DESC
) lease
WHERE p.BrokerId IS NOT NULL AND p.IsDeleted = 0;

INSERT INTO AgreementEvents (Id, AgreementId, Type, Note, At)
SELECT NEWID(), Id, 'Created', 'Created from the broker links that existed before agreements.', SYSDATETIMEOFFSET()
FROM ManagementAgreements;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementEvents");

            migrationBuilder.DropTable(
                name: "AgreementProperties");

            migrationBuilder.DropTable(
                name: "ManagementAgreements");
        }
    }
}
