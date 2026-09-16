using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class RideConditionsPerJourney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredCoRiderGenderPolicy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredDriverGenderPolicy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredMaxAge",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredMinAge",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "CoRiderGenderPolicy",
                table: "TripSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoRiderGenderPolicy",
                table: "TripSchedules");

            migrationBuilder.AddColumn<int>(
                name: "PreferredCoRiderGenderPolicy",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredDriverGenderPolicy",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredMaxAge",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredMinAge",
                table: "Users",
                type: "int",
                nullable: true);
        }
    }
}
