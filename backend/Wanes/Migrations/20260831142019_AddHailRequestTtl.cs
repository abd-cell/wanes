using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class AddHailRequestTtl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 10 == MatchRules.DefaultHailTtlMinutes, written out rather than
            // referenced: a migration is a frozen record of a schema change, and
            // must not start meaning something else when the constant is retuned.
            // Zero would be worse than wrong here — the existing settings row
            // would come back as a hail that expires the instant it opens.
            migrationBuilder.AddColumn<int>(
                name: "HailRequestTtlMinutes",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HailRequestTtlMinutes",
                table: "AppConfigurations");
        }
    }
}
