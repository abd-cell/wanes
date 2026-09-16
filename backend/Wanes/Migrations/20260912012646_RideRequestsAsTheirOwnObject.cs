using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Wanes.Migrations
{
    /// <summary>
    /// v2, §2 and §7: demand stops being a Trip row with three null columns and
    /// becomes its own object.
    ///
    /// The schema half is three new tables. The half that matters is the data
    /// move at the bottom of <c>Up</c>: every <c>Trips</c> row still sitting at
    /// <c>AwaitingDriver</c> is a rider's unmet ask, and it belongs in
    /// <c>RideRequests</c> with its <c>Pending</c> bookings as participations.
    /// Left where they were, those rows would be trips with no driver that every
    /// query in the new world has stopped excluding — which is exactly the
    /// ambiguity v2 exists to remove.
    ///
    /// **This is the one place a request's id changes.** The old trip ids are not
    /// preserved (they are Trips identities), so <c>LegacyTripId</c> carries the
    /// mapping through the move and is dropped at the end. Nothing in the product
    /// reads it; it exists so participations land on the right request and so a
    /// failed run can be diagnosed against the rows it came from.
    ///
    /// Matched requests are not migrated, because there were none: before this
    /// migration a claim mutated the trip in place, so a claimed posting is
    /// already an ordinary trip and stays one, with its bookings intact.
    /// </summary>
    public partial class RideRequestsAsTheirOwnObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverSelectionWindowMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RideRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    Route = table.Column<LineString>(type: "geography", nullable: true),
                    DepartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    SeatsRequested = table.Column<int>(type: "int", nullable: false),
                    GenderPolicy = table.Column<int>(type: "int", nullable: false),
                    DriverGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MatchedTripId = table.Column<int>(type: "int", nullable: true),
                    FirstInterestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideRequests_Trips_MatchedTripId",
                        column: x => x.MatchedTripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RideRequestId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    PricePerSeat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverInterests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverInterests_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DriverInterests_Users_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverInterests_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RideRequestParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RideRequestId = table.Column<int>(type: "int", nullable: false),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CoRiderGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideRequestParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideRequestParticipants_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RideRequestParticipants_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverInterests_DriverId",
                table: "DriverInterests",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverInterests_RideRequestId_DriverId",
                table: "DriverInterests",
                columns: new[] { "RideRequestId", "DriverId" },
                unique: true,
                filter: "[Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DriverInterests_VehicleId",
                table: "DriverInterests",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequestParticipants_RideRequestId_RiderId",
                table: "RideRequestParticipants",
                columns: new[] { "RideRequestId", "RiderId" },
                unique: true,
                filter: "[Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequestParticipants_RiderId",
                table: "RideRequestParticipants",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_MatchedTripId",
                table: "RideRequests",
                column: "MatchedTripId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status_DepartAt",
                table: "RideRequests",
                columns: new[] { "Status", "DepartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status_FirstInterestAt",
                table: "RideRequests",
                columns: new[] { "Status", "FirstInterestAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status_NotifiedAt",
                table: "RideRequests",
                columns: new[] { "Status", "NotifiedAt" });

            // ── The data move ────────────────────────────────────────────────
            //
            // Ordered so nothing is orphaned at any point: requests, then their
            // participants, then the rows they replace.

            migrationBuilder.Sql(
                "ALTER TABLE RideRequests ADD LegacyTripId INT NULL;");

            // AwaitingDriver = 8. Every one of these is a rider's ask that no
            // driver ever took — a claimed one became an ordinary trip in place
            // and is not ours to move.
            //
            // SeatsTotal on such a row was "seats wanted" (the car had not been
            // chosen), which is precisely SeatsRequested. The time window was not
            // a concept before v2, so these take the marketplace default of 30
            // minutes rather than pretending their riders stated one.
            migrationBuilder.Sql(@"
                INSERT INTO RideRequests
                    (OriginAddress, Origin, DestinationAddress, Destination, Route,
                     DepartAt, TimeWindowMinutes, SeatsRequested,
                     GenderPolicy, DriverGenderPolicy, MinAge, MaxAge,
                     RadiusMeters, NotifiedAt, Status, MatchedTripId, FirstInterestAt,
                     IsDeleted, CreationDate, ModificationDate, DeletionDate,
                     CreatedBy, ModifiedBy, LegacyTripId)
                SELECT t.OriginAddress, t.Origin, t.DestinationAddress, t.Destination, t.Route,
                       t.DepartAt, 30, t.SeatsTotal,
                       t.GenderPolicy, t.DriverGenderPolicy, t.MinAge, t.MaxAge,
                       t.RadiusMeters, t.NotifiedAt, 1, NULL, NULL,
                       t.IsDeleted, t.CreationDate, t.ModificationDate, t.DeletionDate,
                       t.CreatedBy, t.ModifiedBy, t.Id
                FROM   Trips t
                WHERE  t.Status = 8;");

            // Pending is the only live seat status a driverless trip could carry,
            // and it is what a participation is. A cancelled seat becomes a Left
            // participation so 'who asked for this' survives the move.
            migrationBuilder.Sql(@"
                INSERT INTO RideRequestParticipants
                    (RideRequestId, RiderId, Seats, Status,
                     CoRiderGenderPolicy, MinAge, MaxAge,
                     IsDeleted, CreationDate, ModificationDate, DeletionDate,
                     CreatedBy, ModifiedBy)
                SELECT r.Id, b.RiderId, b.Seats,
                       CASE WHEN b.Status = 1 THEN 1 ELSE 2 END,
                       b.CoRiderGenderPolicy, b.MinAge, b.MaxAge,
                       b.IsDeleted, b.CreationDate, b.ModificationDate, b.DeletionDate,
                       b.CreatedBy, b.ModifiedBy
                FROM   Bookings b
                JOIN   RideRequests r ON r.LegacyTripId = b.TripId;");

            // The rows they replace. Bookings before trips (the FK points that
            // way), and the status history with them — a trip that no longer
            // exists has no history to keep, and the demand it was is now
            // recorded as a RideRequest in its own right.
            migrationBuilder.Sql(@"
                DELETE b FROM Bookings b
                JOIN   RideRequests r ON r.LegacyTripId = b.TripId;");

            migrationBuilder.Sql(@"
                DELETE h FROM TripStatusHistories h
                JOIN   RideRequests r ON r.LegacyTripId = h.TripId;");

            migrationBuilder.Sql(@"
                DELETE t FROM Trips t
                JOIN   RideRequests r ON r.LegacyTripId = t.Id;");

            migrationBuilder.Sql(
                "ALTER TABLE RideRequests DROP COLUMN LegacyTripId;");
        }

        /// <inheritdoc />
        /// <summary>
        /// Drops the tables, and with them the demand that lived only here.
        ///
        /// Deliberately not reconstructed as AwaitingDriver trips: rebuilding
        /// them would mint new Trips ids, so the rows coming back would not be
        /// the rows that left, and anything that had since referenced a request
        /// would point at a stranger. A down-migration that silently invents
        /// identities is worse than one that is honest about losing them — take
        /// a backup before going back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverInterests");

            migrationBuilder.DropTable(
                name: "RideRequestParticipants");

            migrationBuilder.DropTable(
                name: "RideRequests");

            migrationBuilder.DropColumn(
                name: "DriverSelectionWindowMinutes",
                table: "AppConfigurations");
        }
    }
}
