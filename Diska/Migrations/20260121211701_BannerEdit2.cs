using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diska.Migrations
{
    /// <inheritdoc />
    public partial class BannerEdit2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners");

            migrationBuilder.AlterColumn<string>(
                name: "MerchantId",
                table: "Banners",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners",
                column: "MerchantId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners");

            migrationBuilder.AlterColumn<string>(
                name: "MerchantId",
                table: "Banners",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Banners_AspNetUsers_MerchantId",
                table: "Banners",
                column: "MerchantId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
