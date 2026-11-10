using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateUnusedServiceTypes : Migration
    {
        // Out of the tenant's picker: plumbing, electrical, AC and other stay. Deactivated, not
        // deleted, so requests already raised under them keep their category.
        private const string Codes = "'cleaning', 'paintRepair', 'appliances', 'carpentry'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE MaintenanceServiceTypes SET IsActive = 0, UpdatedAt = SYSDATETIMEOFFSET() WHERE Code IN ({Codes});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE MaintenanceServiceTypes SET IsActive = 1, UpdatedAt = SYSDATETIMEOFFSET() WHERE Code IN ({Codes});");
        }
    }
}
