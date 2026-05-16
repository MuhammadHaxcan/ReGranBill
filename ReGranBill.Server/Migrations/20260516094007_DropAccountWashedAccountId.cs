using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGranBill.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropAccountWashedAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_accounts_WashedAccountId",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_WashedAccountId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "WashedAccountId",
                table: "accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WashedAccountId",
                table: "accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_WashedAccountId",
                table: "accounts",
                column: "WashedAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_accounts_WashedAccountId",
                table: "accounts",
                column: "WashedAccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
