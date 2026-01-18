using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diska.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedDateToActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ActionDate",
                table: "PendingMerchantActions",
                newName: "ProcessedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProcessedDate",
                table: "PendingMerchantActions",
                newName: "ActionDate");
        }
    }
}
