using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diska.Migrations
{
    /// <inheritdoc />
    public partial class EditDealTranslate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "GroupDeals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitleEn",
                table: "GroupDeals");
        }
    }
}
