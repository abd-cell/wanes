using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class RiderTripsConditionsAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RideRequestId",
                table: "Bookings",
                newName: "RiderTripId");

            migrationBuilder.RenameColumn(
                name: "HailRequestTtlMinutes",
                table: "AppConfigurations",
                newName: "PriceAcceptMinutes");

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

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmPromptedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenderPolicy",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinAge",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinSeatsToConfirm",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OccurrenceDate",
                table: "Trips",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleId",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            // The shipped defaults, not zero: this backfills the settings row that
            // already exists, and a speed of zero would make every duration
            // estimate meaningless until an admin happened to save the screen.
            migrationBuilder.AddColumn<double>(
                name: "AverageSpeedKmh",
                table: "AppConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 35.0);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmCutoffMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmDecisionLeadMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.CreateTable(
                name: "TripSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    OwnerRole = table.Column<int>(type: "int", nullable: false),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "int", nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    TimeOfDay = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    PricePerSeat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    MinSeatsToConfirm = table.Column<int>(type: "int", nullable: false),
                    GenderPolicy = table.Column<int>(type: "int", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    MaterialisedThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripSchedules_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripSchedules_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiderTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    DepartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeatsWanted = table.Column<int>(type: "int", nullable: false),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    DriverGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    CoRiderGenderPolicy = table.Column<int>(type: "int", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClaimedTripId = table.Column<int>(type: "int", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduleId = table.Column<int>(type: "int", nullable: true),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    RiderTripId = table.Column<int>(type: "int", nullable: false),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    IsReleased = table.Column<bool>(type: "bit", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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

            // ── Carrying the hails over ──
            //
            // A hail was a rider-posted trip that could only ever mean "now", so
            // these are the same rows and are moved rather than dropped. Ids are
            // preserved with IDENTITY_INSERT because Bookings.RiderTripId (renamed
            // above, not re-pointed) still holds them — recreating the rows with
            // fresh ids would leave every hail-born booking pointing at a
            // stranger, and the foreign key added at the end of this migration
            // would refuse to be created at all.
            //
            // Two columns have no counterpart and are deliberately dropped:
            // RequestedAt (a posting is dated by CreationDate) and ExpiresAt (a
            // posting now runs until its departure, not on a countdown).
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT RiderTrips ON;

                INSERT INTO RiderTrips
                    (Id, RiderId, OriginAddress, Origin, DestinationAddress, Destination,
                     DepartAt, SeatsWanted, RadiusMeters, DriverGenderPolicy, CoRiderGenderPolicy,
                     MinAge, MaxAge, Status, ClaimedTripId, NotifiedAt, ScheduleId, OccurrenceDate,
                     IsDeleted, CreationDate, ModificationDate, DeletionDate, CreatedBy, ModifiedBy)
                SELECT
                    Id, RiderId, OriginAddress, Origin, DestinationAddress, Destination,
                    WantedDepartAt, Seats, RadiusMeters, 0, 0,
                    NULL, NULL, Status, MatchedTripId, NULL, NULL, NULL,
                    IsDeleted, CreationDate, ModificationDate, DeletionDate, CreatedBy, ModifiedBy
                FROM RideRequests;

                SET IDENTITY_INSERT RiderTrips OFF;

                -- The rider who opened a hail holds its seats, exactly as the
                -- author of a posting does now. Without this the carried-over
                -- rows would show nobody aboard and be cancelled by the first
                -- person who left them.
                INSERT INTO RiderTripHolds
                    (RiderTripId, RiderId, Seats, IsReleased, CoRiderGenderPolicy,
                     MinAge, MaxAge, IsDeleted, CreationDate)
                SELECT Id, RiderId, SeatsWanted, 0, 0, NULL, NULL, IsDeleted, CreationDate
                FROM RiderTrips;
            ");

            migrationBuilder.DropTable(
                name: "RideRequests");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ScheduleId_OccurrenceDate",
                table: "Trips",
                columns: new[] { "ScheduleId", "OccurrenceDate" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RiderTripId",
                table: "Bookings",
                column: "RiderTripId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_HoldExpiresAt",
                table: "Bookings",
                columns: new[] { "Status", "HoldExpiresAt" });

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

            migrationBuilder.CreateIndex(
                name: "IX_TripSchedules_IsPaused_MaterialisedThrough",
                table: "TripSchedules",
                columns: new[] { "IsPaused", "MaterialisedThrough" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSchedules_OwnerId",
                table: "TripSchedules",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSchedules_VehicleId",
                table: "TripSchedules",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_RiderTrips_RiderTripId",
                table: "Bookings",
                column: "RiderTripId",
                principalTable: "RiderTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_TripSchedules_ScheduleId",
                table: "Trips",
                column: "ScheduleId",
                principalTable: "TripSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_RiderTrips_RiderTripId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_TripSchedules_ScheduleId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_ScheduleId_OccurrenceDate",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RiderTripId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_HoldExpiresAt",
                table: "Bookings");

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

            migrationBuilder.DropColumn(
                name: "ConfirmPromptedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "GenderPolicy",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "MaxAge",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "MinAge",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "MinSeatsToConfirm",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "OccurrenceDate",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AverageSpeedKmh",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ConfirmCutoffMinutes",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ConfirmDecisionLeadMinutes",
                table: "AppConfigurations");

            migrationBuilder.RenameColumn(
                name: "RiderTripId",
                table: "Bookings",
                newName: "RideRequestId");

            migrationBuilder.RenameColumn(
                name: "PriceAcceptMinutes",
                table: "AppConfigurations",
                newName: "HailRequestTtlMinutes");

            migrationBuilder.CreateTable(
                name: "RideRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MatchedTripId = table.Column<int>(type: "int", nullable: true),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WantedDepartAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideRequests_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // The rows go back the way they came, ids and all, so a rollback
            // leaves Bookings.RideRequestId pointing at the same requests it did
            // before. ExpiresAt cannot be recovered — a posting has no countdown
            // — so it comes back null, which reads as "no deadline set" and is
            // the truthful answer rather than an invented one.
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT RideRequests ON;

                INSERT INTO RideRequests
                    (Id, RiderId, OriginAddress, Origin, DestinationAddress, Destination,
                     RequestedAt, WantedDepartAt, Seats, RadiusMeters, Status, MatchedTripId,
                     ExpiresAt, IsDeleted, CreationDate, ModificationDate, DeletionDate,
                     CreatedBy, ModifiedBy)
                SELECT
                    Id, RiderId, OriginAddress, Origin, DestinationAddress, Destination,
                    CreationDate, DepartAt, SeatsWanted, RadiusMeters, Status, ClaimedTripId,
                    NULL, IsDeleted, CreationDate, ModificationDate, DeletionDate,
                    CreatedBy, ModifiedBy
                FROM RiderTrips
                WHERE ScheduleId IS NULL;

                SET IDENTITY_INSERT RideRequests OFF;
            ");

            migrationBuilder.DropTable(
                name: "RiderTripHolds");

            migrationBuilder.DropTable(
                name: "RiderTrips");

            migrationBuilder.DropTable(
                name: "TripSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_RiderId",
                table: "RideRequests",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status",
                table: "RideRequests",
                column: "Status");
        }
    }
}
