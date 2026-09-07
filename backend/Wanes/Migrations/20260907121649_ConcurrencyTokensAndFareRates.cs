using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrencyTokensAndFareRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Trips",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RideRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // Seeded with the shipped rates, not 0m. There is one settings row and
            // it already exists on every install, so a zero default would not sit
            // unused waiting to be configured — it would immediately price every
            // hail-accepted trip at nothing, and look like a deliberate "free".
            migrationBuilder.AddColumn<decimal>(
                name: "FareBaseAmount",
                table: "AppConfigurations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 2.50m);

            migrationBuilder.AddColumn<decimal>(
                name: "FarePerKm",
                table: "AppConfigurations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 1.20m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "FareBaseAmount",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "FarePerKm",
                table: "AppConfigurations");
        }
    }
}
