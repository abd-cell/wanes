using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wanes.Migrations
{
    /// <inheritdoc />
    public partial class UniquePhoneKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A database carrying duplicate keys would fail on CREATE INDEX with a message that
            // names neither the column values nor what to do about them. Fail first, and say which
            // accounts have to be merged by hand — the merge is a judgement call about whose trips,
            // vehicles and bookings survive, so it cannot be automated here.
            migrationBuilder.Sql(@"
DECLARE @dupes NVARCHAR(MAX);
SELECT @dupes = STRING_AGG([PhoneKey], ', ')
FROM (SELECT [PhoneKey] FROM [Users] WHERE [IsDeleted] = 0
      GROUP BY [PhoneKey] HAVING COUNT(*) > 1) d;
IF @dupes IS NOT NULL
BEGIN
    DECLARE @msg NVARCHAR(2048) = N'Cannot make Users.PhoneKey unique: several live accounts share a phone key. Merge them first, then re-run. Affected keys: ' + @dupes;
    THROW 50001, @msg, 1;
END
");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneKey",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneKey",
                table: "Users",
                column: "PhoneKey",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneKey",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneKey",
                table: "Users",
                column: "PhoneKey");
        }
    }
}
