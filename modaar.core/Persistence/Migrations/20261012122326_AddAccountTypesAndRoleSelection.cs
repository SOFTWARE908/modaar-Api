using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypesAndRoleSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.AddColumn<string>(
                name: "AccountTypes",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ActiveRole",
                table: "RefreshTokens",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // 2. Carry every existing user's single role into a one-element array.
            //    EF does not generate this; without it the drop below loses the data.
            migrationBuilder.Sql(
                "UPDATE Users SET AccountTypes = '[\"' + AccountType + '\"]' " +
                "WHERE AccountType IS NOT NULL AND AccountType <> '';");


            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Users");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE Users SET AccountType = ISNULL(JSON_VALUE(AccountTypes, '$[0]'), '');");

            migrationBuilder.DropColumn(
                name: "AccountTypes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActiveRole",
                table: "RefreshTokens");

        }
    }
}
