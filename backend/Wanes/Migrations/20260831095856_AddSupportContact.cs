using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "AppConfigurations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportHours",
                table: "AppConfigurations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportPhone",
                table: "AppConfigurations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportWebsite",
                table: "AppConfigurations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportWhatsApp",
                table: "AppConfigurations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportEmail",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SupportHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SupportPhone",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SupportWebsite",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SupportWhatsApp",
                table: "AppConfigurations");
        }
    }
}
