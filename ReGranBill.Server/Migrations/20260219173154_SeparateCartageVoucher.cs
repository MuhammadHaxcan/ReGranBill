using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGranBill.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeparateCartageVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers");

            migrationBuilder.CreateIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers",
                column: "DcId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers");

            migrationBuilder.CreateIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers",
                column: "DcId",
                unique: true);
        }
    }
}
