using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <summary>
    /// v2, §6: a trip's state stops being one ladder.
    ///
    /// Confirmation becomes its own stored dimension (<c>Trips.ConfirmedAt</c>)
    /// rather than a function of the live seat count, and capacity leaves the
    /// status column altogether — <c>Full</c> was never a place a trip *was*, it
    /// was a fact about its seats, and it is the one condition that moves back.
    ///
    /// Both halves need a backfill, and neither is optional. A confirmed trip
    /// with no stamp would read as gathering and prompt its driver again at the
    /// cutoff; a row left at <c>Full</c> would sit outside every lifecycle
    /// query written against Posted.
    /// </summary>
    public partial class TripConfirmationDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumPassengersDefault",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 3);

            // The marketplace's threshold for rows written before it existed.
            // AddColumn's default only covers inserts.
            migrationBuilder.Sql(
                "UPDATE AppConfigurations SET MinimumPassengersDefault = 3 " +
                "WHERE MinimumPassengersDefault <= 0;");

            // A trip that already committed somebody is confirmed, and has been
            // since that seat was committed — Pending is the only booking status
            // that means "held but not promised", so anything past it is proof.
            // Stamped with the seat's own timestamp rather than now: the stamp is
            // read as "when these riders were told", and dating it to the
            // migration would put every historical trip's confirmation today.
            migrationBuilder.Sql(@"
                UPDATE t
                SET    t.ConfirmedAt = c.ConfirmedAt
                FROM   Trips t
                JOIN  (SELECT b.TripId,
                              MIN(COALESCE(b.ModificationDate, b.CreationDate)) AS ConfirmedAt
                       FROM   Bookings b
                       WHERE  b.Status IN (2, 3, 4, 6)   -- Confirmed, InProgress, Completed, Arrived
                       GROUP BY b.TripId) c ON c.TripId = t.Id
                WHERE  t.ConfirmedAt IS NULL;");

            // Capacity is derived from SeatsLeft now, so Full has no meaning in
            // this column. Only the two live lifecycle values are rewritten —
            // a cancelled or completed trip is terminal and its history is not
            // ours to tidy.
            migrationBuilder.Sql("UPDATE Trips SET Status = 1 WHERE Status = 2;");
            migrationBuilder.Sql("UPDATE TripStatusHistories SET Status = 1 WHERE Status = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Full is restored from the seats, which is the only place it was
            // ever really recorded. The stamp is simply dropped: a trip's
            // confirmation goes back to being derived, and the old derivation
            // reads it off the bookings that are still there.
            migrationBuilder.Sql(
                "UPDATE Trips SET Status = 2 WHERE Status = 1 AND SeatsLeft <= 0;");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "MinimumPassengersDefault",
                table: "AppConfigurations");
        }
    }
}
