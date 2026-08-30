using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_Phone",
                table: "OtpCodes");

            migrationBuilder.AddColumn<string>(
                name: "PhoneKey",
                table: "Users",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneKey",
                table: "OtpCodes",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows so the new key lines up with the phones already on file.
            const string digits =
                "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({0}, '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')";
            migrationBuilder.Sql($"UPDATE [Users] SET [PhoneKey] = RIGHT({string.Format(digits, "[Phone]")}, 9);");
            migrationBuilder.Sql($"UPDATE [OtpCodes] SET [PhoneKey] = RIGHT({string.Format(digits, "[Phone]")}, 9);");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneKey",
                table: "Users",
                column: "PhoneKey");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PhoneKey",
                table: "OtpCodes",
                column: "PhoneKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneKey",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_PhoneKey",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "PhoneKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneKey",
                table: "OtpCodes");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Phone",
                table: "OtpCodes",
                column: "Phone");
        }
    }
}
