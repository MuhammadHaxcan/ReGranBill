using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReGranBill.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryIsEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "journal_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "journal_entries");
        }
    }
}
