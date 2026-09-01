using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableFont : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 == AppFont.Jakarta, written out rather than referenced: a
            // migration is a frozen record of a schema change, and must not
            // start meaning something else if the enum is ever reordered.
            // EF's own default of 0 would be actively wrong — no AppFont has
            // that value, so the existing settings row would come back as a
            // font no client can resolve.
            migrationBuilder.AddColumn<int>(
                name: "FontFamily",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FontFamily",
                table: "AppConfigurations");
        }
    }
}
