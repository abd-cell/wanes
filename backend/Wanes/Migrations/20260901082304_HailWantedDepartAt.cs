using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class HailWantedDepartAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WantedDepartAt",
                table: "RideRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            // Every hail that already exists was opened under the old rule, where
            // a hail meant "leaving now" and RequestedAt was what the reverse
            // match and the driver's card read as its time. Carrying that value
            // across keeps those rows saying exactly what they used to say --
            // the column default would otherwise stamp them all 0001-01-01 and
            // put every historic hail outside every matching window.
            migrationBuilder.Sql("UPDATE [RideRequests] SET [WantedDepartAt] = [RequestedAt];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WantedDepartAt",
                table: "RideRequests");
        }
    }
}
