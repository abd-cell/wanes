using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousRefreshTokenHash",
                table: "UserLogins",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshRotatedAt",
                table: "UserLogins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "UserLogins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "UserLogins",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_RefreshTokenHash",
                table: "UserLogins",
                column: "RefreshTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLogins_RefreshTokenHash",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "PreviousRefreshTokenHash",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "RefreshRotatedAt",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "UserLogins");
        }
    }
}
