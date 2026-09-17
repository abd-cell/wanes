using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class SeriesCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesCommitmentId",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecurringOnly",
                table: "DemandAlerts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SeriesCommitmentId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeriesCommitmentsEnabled",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SeriesDecisionHours",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeriesEndNoticeDays",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeriesFreeSkipsPerWindow",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeriesSkipNoticeHours",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeriesSummaryDay",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SeriesCommitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    RiderId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    PricePerSeat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Seats = table.Column<int>(type: "int", nullable: true),
                    DaysOfWeek = table.Column<int>(type: "int", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DecideAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedBy = table.Column<int>(type: "int", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndReason = table.Column<int>(type: "int", nullable: true),
                    EndNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SharedTermsAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSummaryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesCommitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesCommitments_TripSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "TripSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesCommitments_Users_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesCommitments_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesCommitments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_SeriesCommitmentId",
                table: "Trips",
                column: "SeriesCommitmentId",
                filter: "[SeriesCommitmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SeriesCommitmentId",
                table: "Bookings",
                column: "SeriesCommitmentId",
                filter: "[SeriesCommitmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCommitments_DriverId",
                table: "SeriesCommitments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCommitments_RiderId",
                table: "SeriesCommitments",
                column: "RiderId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCommitments_ScheduleId_Side_Status",
                table: "SeriesCommitments",
                columns: new[] { "ScheduleId", "Side", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCommitments_Status_DecideAt",
                table: "SeriesCommitments",
                columns: new[] { "Status", "DecideAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCommitments_VehicleId",
                table: "SeriesCommitments",
                column: "VehicleId");

            // The existing settings row takes the shipped series defaults, not the column defaults.
            migrationBuilder.Sql(@"UPDATE [AppConfigurations] SET
    [SeriesCommitmentsEnabled] = 1,
    [SeriesDecisionHours] = 12,
    [SeriesSkipNoticeHours] = 24,
    [SeriesFreeSkipsPerWindow] = 4,
    [SeriesEndNoticeDays] = 7,
    [SeriesSummaryDay] = 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeriesCommitments");

            migrationBuilder.DropIndex(
                name: "IX_Trips_SeriesCommitmentId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SeriesCommitmentId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SeriesCommitmentId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RecurringOnly",
                table: "DemandAlerts");

            migrationBuilder.DropColumn(
                name: "SeriesCommitmentId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SeriesCommitmentsEnabled",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SeriesDecisionHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SeriesEndNoticeDays",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SeriesFreeSkipsPerWindow",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SeriesSkipNoticeHours",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "SeriesSummaryDay",
                table: "AppConfigurations");
        }
    }
}
