using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class SharedMarketplacePhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverCancellations",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiderLateCancels",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiderNoShows",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedUntil",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecideAt",
                table: "RideRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReopenedFromRequestId",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedTermsAcceptedAt",
                table: "RideRequestParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinPassengers",
                table: "DriverInterests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatsOffered",
                table: "DriverInterests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedTermsAcceptedAt",
                table: "DriverInterests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardingCode",
                table: "Bookings",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Bookings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShareTokenCreatedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedTermsAcceptedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BoardingCodeRequired",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyNumber",
                table: "AppConfigurations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FreeCancelGraceMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateCancelLeadMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReliabilitySuspendPoints",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReliabilityWarnPoints",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReliabilityWindowDays",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireSharedTermsAcceptance",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RiderOfferChoice",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledSelectionWindowMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShareBaseUrl",
                table: "AppConfigurations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspensionDays",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DemandAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    OriginAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origin = table.Column<Point>(type: "geography", nullable: false),
                    DestinationAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Destination = table.Column<Point>(type: "geography", nullable: false),
                    RadiusMeters = table.Column<int>(type: "int", nullable: false),
                    MinSeats = table.Column<int>(type: "int", nullable: false),
                    RideRequestId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotifiedCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandAlerts_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandAlerts_Users_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReliabilityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RidersAffected = table.Column<int>(type: "int", nullable: false),
                    MinutesBeforeDeparture = table.Column<int>(type: "int", nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false),
                    WaivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaivedBy = table.Column<int>(type: "int", nullable: true),
                    WaiveNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReliabilityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReliabilityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<Point>(type: "geography", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmergencyContactNotified = table.Column<bool>(type: "bit", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    HandledBy = table.Column<int>(type: "int", nullable: true),
                    HandledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyIncidents_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyIncidents_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAcknowledgements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandAlertHits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DemandAlertId = table.Column<int>(type: "int", nullable: false),
                    RideRequestId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandAlertHits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandAlertHits_DemandAlerts_DemandAlertId",
                        column: x => x.DemandAlertId,
                        principalTable: "DemandAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status_DecideAt",
                table: "RideRequests",
                columns: new[] { "Status", "DecideAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ShareToken",
                table: "Bookings",
                column: "ShareToken",
                unique: true,
                filter: "[ShareToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DemandAlertHits_DemandAlertId_RideRequestId",
                table: "DemandAlertHits",
                columns: new[] { "DemandAlertId", "RideRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandAlerts_DriverId",
                table: "DemandAlerts",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandAlerts_IsActive_RideRequestId",
                table: "DemandAlerts",
                columns: new[] { "IsActive", "RideRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandAlerts_RideRequestId",
                table: "DemandAlerts",
                column: "RideRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_UserId_CreationDate",
                table: "ReliabilityEvents",
                columns: new[] { "UserId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyIncidents_ReporterId",
                table: "SafetyIncidents",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyIncidents_Status_CreationDate",
                table: "SafetyIncidents",
                columns: new[] { "Status", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyIncidents_TripId",
                table: "SafetyIncidents",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAcknowledgements_UserId_Kind_Version",
                table: "UserAcknowledgements",
                columns: new[] { "UserId", "Kind", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // The existing settings row takes the shipped defaults rather than
            // the zeros a new NOT NULL column starts with.
            migrationBuilder.Sql(@"UPDATE [AppConfigurations] SET
    [ScheduledSelectionWindowMinutes] = 20,
    [RiderOfferChoice] = 1,
    [RequireSharedTermsAcceptance] = 1,
    [FreeCancelGraceMinutes] = 3,
    [LateCancelLeadMinutes] = 120,
    [ReliabilityWarnPoints] = 3,
    [ReliabilitySuspendPoints] = 5,
    [ReliabilityWindowDays] = 30,
    [SuspensionDays] = 7,
    [BoardingCodeRequired] = 1,
    [EmergencyNumber] = N'911';");

            // Requests already holding offers are decided on the schedule they
            // were given: immediately.
            migrationBuilder.Sql(@"UPDATE [RideRequests] SET [DecideAt] = [FirstInterestAt]
WHERE [DecideAt] IS NULL AND [FirstInterestAt] IS NOT NULL;");        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandAlertHits");

            migrationBuilder.DropTable(
                name: "ReliabilityEvents");

            migrationBuilder.DropTable(
                name: "SafetyIncidents");

            migrationBuilder.DropTable(
                name: "UserAcknowledgements");

            migrationBuilder.DropTable(
                name: "DemandAlerts");

            migrationBuilder.DropIndex(
                name: "IX_RideRequests_Status_DecideAt",
                table: "RideRequests");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ShareToken",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DriverCancellations",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RiderLateCancels",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RiderNoShows",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DecideAt",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "ReopenedFromRequestId",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "SharedTermsAcceptedAt",
                table: "RideRequestParticipants");

            migrationBuilder.DropColumn(
                name: "MinPassengers",
                table: "DriverInterests");

            migrationBuilder.DropColumn(
                name: "SeatsOffered",
                table: "DriverInterests");

            migrationBuilder.DropColumn(
                name: "SharedTermsAcceptedAt",
                table: "DriverInterests");

            migrationBuilder.DropColumn(
                name: "BoardingCode",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ShareTokenCreatedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SharedTermsAcceptedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BoardingCodeRequired",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "EmergencyNumber",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "FreeCancelGraceMinutes",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "LateCancelLeadMinutes",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ReliabilitySuspendPoints",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ReliabilityWarnPoints",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ReliabilityWindowDays",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireSharedTermsAcceptance",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "RiderOfferChoice",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ScheduledSelectionWindowMinutes",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ShareBaseUrl",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SuspensionDays",
                table: "AppConfigurations");
        }
    }
}
