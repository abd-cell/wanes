using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class RideRequestOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "OccurrenceDate",
                table: "RideRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleId",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_ScheduleId_OccurrenceDate",
                table: "RideRequests",
                columns: new[] { "ScheduleId", "OccurrenceDate" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_RideRequests_TripSchedules_ScheduleId",
                table: "RideRequests",
                column: "ScheduleId",
                principalTable: "TripSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RideRequests_TripSchedules_ScheduleId",
                table: "RideRequests");

            migrationBuilder.DropIndex(
                name: "IX_RideRequests_ScheduleId_OccurrenceDate",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "OccurrenceDate",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "RideRequests");
        }
    }
}
