using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReGranBill.Server.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateDcIntoJv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_vouchers_delivery_challans_DcId",
                table: "journal_vouchers");

            migrationBuilder.DropTable(
                name: "dc_cartage");

            migrationBuilder.DropTable(
                name: "dc_lines");

            migrationBuilder.DropTable(
                name: "delivery_challans");

            migrationBuilder.DropIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers");

            migrationBuilder.DropColumn(
                name: "DcId",
                table: "journal_vouchers");

            migrationBuilder.AddColumn<string>(
                name: "VehicleNumber",
                table: "journal_vouchers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "journal_entries",
                type: "numeric(12,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VehicleNumber",
                table: "journal_vouchers");

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "journal_entries");

            migrationBuilder.AddColumn<int>(
                name: "DcId",
                table: "journal_vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_challans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DcNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RatesAdded = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VehicleNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VoucherType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_challans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_challans_accounts_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_delivery_challans_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dc_cartage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DcId = table.Column<int>(type: "integer", nullable: false),
                    TransporterId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dc_cartage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dc_cartage_accounts_TransporterId",
                        column: x => x.TransporterId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dc_cartage_delivery_challans_DcId",
                        column: x => x.DcId,
                        principalTable: "delivery_challans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dc_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DcId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Rbp = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dc_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dc_lines_accounts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dc_lines_delivery_challans_DcId",
                        column: x => x.DcId,
                        principalTable: "delivery_challans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_vouchers_DcId",
                table: "journal_vouchers",
                column: "DcId");

            migrationBuilder.CreateIndex(
                name: "IX_dc_cartage_DcId",
                table: "dc_cartage",
                column: "DcId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dc_cartage_TransporterId",
                table: "dc_cartage",
                column: "TransporterId");

            migrationBuilder.CreateIndex(
                name: "IX_dc_lines_DcId",
                table: "dc_lines",
                column: "DcId");

            migrationBuilder.CreateIndex(
                name: "IX_dc_lines_ProductId",
                table: "dc_lines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_challans_CreatedBy",
                table: "delivery_challans",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_challans_CustomerId",
                table: "delivery_challans",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_challans_DcNumber",
                table: "delivery_challans",
                column: "DcNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_vouchers_delivery_challans_DcId",
                table: "journal_vouchers",
                column: "DcId",
                principalTable: "delivery_challans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
