using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveBookingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_user_id",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_user_id_status",
                table: "bookings",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_user_id_status",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_user_id",
                table: "bookings",
                column: "user_id");
        }
    }
}
