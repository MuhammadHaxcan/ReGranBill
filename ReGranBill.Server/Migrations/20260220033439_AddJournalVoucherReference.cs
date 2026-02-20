using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReGranBill.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalVoucherReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_voucher_references",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MainVoucherId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceVoucherId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_voucher_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journal_voucher_references_journal_vouchers_MainVoucherId",
                        column: x => x.MainVoucherId,
                        principalTable: "journal_vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_journal_voucher_references_journal_vouchers_ReferenceVouche~",
                        column: x => x.ReferenceVoucherId,
                        principalTable: "journal_vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_voucher_references_MainVoucherId_ReferenceVoucherId",
                table: "journal_voucher_references",
                columns: new[] { "MainVoucherId", "ReferenceVoucherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_voucher_references_ReferenceVoucherId",
                table: "journal_voucher_references",
                column: "ReferenceVoucherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_voucher_references");
        }
    }
}
