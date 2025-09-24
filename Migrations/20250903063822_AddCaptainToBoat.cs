using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FishingLebanon.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptainToBoat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CaptainId",
                table: "Boats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Boats_CaptainId",
                table: "Boats",
                column: "CaptainId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boats_Users_CaptainId",
                table: "Boats",
                column: "CaptainId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boats_Users_CaptainId",
                table: "Boats");

            migrationBuilder.DropIndex(
                name: "IX_Boats_CaptainId",
                table: "Boats");

            migrationBuilder.DropColumn(
                name: "CaptainId",
                table: "Boats");
        }
    }
}
