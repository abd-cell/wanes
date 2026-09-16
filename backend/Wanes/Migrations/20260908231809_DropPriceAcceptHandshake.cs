using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// A claim now confirms the riders' seats outright, exactly as booking a
    /// driver's own trip does — so the price-acceptance handshake, and the two
    /// columns that carried it, are gone. A rider who does not like the figure
    /// leaves; that was always the escape hatch, and it needed no second one.
    /// </summary>
    public partial class DropPriceAcceptHandshake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_HoldExpiresAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PriceAcceptMinutes",
                table: "AppConfigurations");

            // Any seat still waiting on a price when this ships is a seat whose
            // rider asked for the ride and whose driver is making it. Confirm
            // them rather than leaving them Pending: with the handshake gone
            // nothing would ever resolve them, and the trip's own seat
            // threshold — the only remaining producer of a pending seat — never
            // put them there.
            migrationBuilder.Sql(@"
                UPDATE b
                SET b.Status = 2   -- Confirmed
                FROM Bookings b
                JOIN Trips t ON t.Id = b.TripId
                WHERE b.Status = 1 -- Pending
                  AND b.RiderTripId IS NOT NULL
                  AND t.MinSeatsToConfirm <= 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceAcceptMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_HoldExpiresAt",
                table: "Bookings",
                columns: new[] { "Status", "HoldExpiresAt" });
        }
    }
}
