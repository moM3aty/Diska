using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diska.Migrations
{
    /// <inheritdoc />
    public partial class AddBanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminComment",
                table: "Banners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Banners",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MerchantId",
                table: "Banners",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_MerchantId",
                table: "Banners",
                column: "MerchantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners",
                column: "MerchantId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners");

            migrationBuilder.DropIndex(
                name: "IX_Banners_MerchantId",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "AdminComment",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "MerchantId",
                table: "Banners");
        }
    }
}
