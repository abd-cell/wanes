using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class MergeRiderTripsIntoTrips : Migration
    {
        /// <summary>
        /// Folds RiderTrips and RiderTripHolds into Trips and Bookings.
        ///
        /// A rider's posting was never a different kind of thing from a trip —
        /// it is a trip nobody is driving yet — so the two tables become one.
        /// After this, Trips.DriverId / VehicleId / PricePerSeat are nullable and
        /// a null driver plus status 8 (AwaitingDriver) *is* a posting.
        ///
        /// The order below is the whole point: **every row is copied before
        /// anything is dropped.** The scaffolded version dropped the tables
        /// first and renamed Bookings.RiderTripId into MinAge, which would have
        /// destroyed 150 postings and written trip ids into an age column.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The new shape, before anything is copied into it ──────────

            migrationBuilder.AlterColumn<int>(
                name: "VehicleId",
                table: "Trips",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DriverId",
                table: "Trips",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DriverGenderPolicy",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotifiedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            // 5000 m, the default a posting has always carried — not 0, which
            // would make every existing trip unreachable by the driver board.
            migrationBuilder.AddColumn<int>(
                name: "RadiusMeters",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 5000);

            migrationBuilder.AddColumn<int>(
                name: "CoRiderGenderPolicy",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinAge",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "Bookings",
                type: "int",
                nullable: true);

            // ── 2. Carry the rows over ───────────────────────────────────────
            //
            // Claimed postings are skipped: the trip they produced already
            // exists and already carries their riders as bookings. What they get
            // instead is a status-log row (step 3), so the admin console can
            // still tell that trip began as a rider's ask.
            //
            // MERGE, not INSERT, because the new trip ids have to be captured
            // against the posting ids they came from — OUTPUT can only see both
            // sides in a MERGE. Statuses map 1 (Open) → 8 (AwaitingDriver), and
            // 3 (Expired) / 4 (Cancelled) → 5 (Cancelled): to everybody involved
            // an expired posting is a trip that is not happening.
            migrationBuilder.Sql(@"
                CREATE TABLE #Moved (RiderTripId INT PRIMARY KEY, TripId INT NOT NULL);

                MERGE INTO Trips AS t
                USING (SELECT * FROM RiderTrips WHERE Status <> 2) AS r
                    ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT (DriverId, VehicleId, PricePerSeat,
                            OriginAddress, Origin, DestinationAddress, Destination, Route,
                            DepartAt, SeatsTotal, SeatsLeft, Status,
                            MinSeatsToConfirm, GenderPolicy, DriverGenderPolicy,
                            MinAge, MaxAge, RadiusMeters, NotifiedAt,
                            ScheduleId, OccurrenceDate,
                            IsDeleted, CreationDate, ModificationDate, DeletionDate,
                            CreatedBy, ModifiedBy)
                    VALUES (NULL, NULL, NULL,
                            r.OriginAddress, r.Origin, r.DestinationAddress, r.Destination, NULL,
                            r.DepartAt, r.SeatsWanted, 0,
                            CASE r.Status WHEN 1 THEN 8 ELSE 5 END,
                            0, r.CoRiderGenderPolicy, r.DriverGenderPolicy,
                            r.MinAge, r.MaxAge, r.RadiusMeters, r.NotifiedAt,
                            r.ScheduleId, r.OccurrenceDate,
                            r.IsDeleted, r.CreationDate, r.ModificationDate, r.DeletionDate,
                            r.CreatedBy, r.ModifiedBy)
                OUTPUT inserted.Id, r.Id INTO #Moved (TripId, RiderTripId);

                -- The holds become the seats they always were. A live hold is a
                -- Pending booking; a released one is Cancelled, which is what
                -- leaving a trip has always meant.
                INSERT INTO Bookings (TripId, RiderId, Seats, Status,
                                      CoRiderGenderPolicy, MinAge, MaxAge,
                                      IsDeleted, CreationDate, ModificationDate, DeletionDate,
                                      CreatedBy, ModifiedBy)
                SELECT m.TripId, h.RiderId, h.Seats,
                       CASE WHEN h.IsReleased = 1 THEN 5 ELSE 1 END,
                       h.CoRiderGenderPolicy, h.MinAge, h.MaxAge,
                       h.IsDeleted, h.CreationDate, h.ModificationDate, h.DeletionDate,
                       h.CreatedBy, h.ModifiedBy
                FROM RiderTripHolds h
                JOIN #Moved m ON m.RiderTripId = h.RiderTripId;

                -- ── 3. The status log ──
                --
                -- Every trip a rider wrote gets an AwaitingDriver row, including
                -- the ones already claimed. This is the only record that a trip
                -- began as demand, and the admin console's demand figures read
                -- it. It is a log, not a flag: nothing in the product branches
                -- on it, so 'one trip type' still holds.
                INSERT INTO TripStatusHistories (TripId, Status, ChangedBy, IsDeleted, CreationDate)
                SELECT m.TripId, 8, NULL, 0, r.CreationDate
                FROM #Moved m JOIN RiderTrips r ON r.Id = m.RiderTripId;

                INSERT INTO TripStatusHistories (TripId, Status, ChangedBy, IsDeleted, CreationDate)
                SELECT r.ClaimedTripId, 8, NULL, 0, r.CreationDate
                FROM RiderTrips r
                WHERE r.Status = 2 AND r.ClaimedTripId IS NOT NULL
                  AND EXISTS (SELECT 1 FROM Trips t WHERE t.Id = r.ClaimedTripId);

                DROP TABLE #Moved;
            ");

            // ── 4. Only now, when nothing needs them ─────────────────────────

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_RiderTrips_RiderTripId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "RiderTripHolds");

            migrationBuilder.DropTable(
                name: "RiderTrips");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RiderTripId",
                table: "Bookings");

            // Dropped, never renamed: the seats it pointed at are on their trip
            // already, and renaming it would have poured rider-trip ids into a
            // brand-new age column.
            migrationBuilder.DropColumn(
                name: "RiderTripId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId_RiderId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Status_NotifiedAt",
                table: "Trips",
                columns: new[] { "Status", "NotifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId_RiderId",
                table: "Bookings",
                columns: new[] { "TripId", "RiderId" },
                unique: true,
                filter: "[Status] <> 4 AND [Status] <> 5 AND [Status] <> 7 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_Status_NotifiedAt",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId_RiderId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DriverGenderPolicy",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "NotifiedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RadiusMeters",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CoRiderGenderPolicy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MinAge",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MaxAge",
                table: "Bookings");

            // The column comes back empty. Down() restores the *shape*, not the
            // two tables' worth of rows — those are in Trips and Bookings now,
            // and a rollback that tried to unpick them would be guessing.
            migrationBuilder.AddColumn<int>(
                name: "RiderTripId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "VehicleId",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DriverId",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RiderTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: true),
                    ClaimedTripId = table.Column<int>(type: "int", nullable: true),
                    CoRiderGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DriverGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SeatsWanted = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiderTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiderTrips_TripSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "TripSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RiderTrips_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiderTripHolds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    RiderTripId = table.Column<int>(type: "int", nullable: false),
                    CoRiderGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsReleased = table.Column<bool>(type: "bit", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Seats = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiderTripHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiderTripHolds_RiderTrips_RiderTripId",
                        column: x => x.RiderTripId,
                        principalTable: "RiderTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiderTripHolds_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RiderTripId",
                table: "Bookings",
                column: "RiderTripId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId_RiderId",
                table: "Bookings",
                columns: new[] { "TripId", "RiderId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiderTripHolds_RiderId",
                table: "RiderTripHolds",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderTripHolds_RiderTripId_RiderId",
                table: "RiderTripHolds",
                columns: new[] { "RiderTripId", "RiderId" },
                unique: true,
                filter: "[IsReleased] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiderTrips_RiderId",
                table: "RiderTrips",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderTrips_ScheduleId_OccurrenceDate",
                table: "RiderTrips",
                columns: new[] { "ScheduleId", "OccurrenceDate" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RiderTrips_Status_DepartAt",
                table: "RiderTrips",
                columns: new[] { "Status", "DepartAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_RiderTrips_RiderTripId",
                table: "Bookings",
                column: "RiderTripId",
                principalTable: "RiderTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
