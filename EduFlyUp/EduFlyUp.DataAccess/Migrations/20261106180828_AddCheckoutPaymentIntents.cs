using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduFlyUp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutPaymentIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutPaymentIntents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckoutOrderId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: false),
                    BankBin = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutPaymentIntents", x => x.Id);
                    table.CheckConstraint("CK_CheckoutPaymentIntents_Amount", "[Amount] > 0 AND [Amount] <= 9999999999999 AND [Amount] = FLOOR([Amount]) AND [Currency] = 'VND'");
                    table.CheckConstraint("CK_CheckoutPaymentIntents_Reference", "LEN([Reference]) = 22 AND LEFT([Reference],2) = 'FU' AND [Reference] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z0-9]%'");
                    table.ForeignKey(
                        name: "FK_CheckoutPaymentIntents_CheckoutOrders_CheckoutOrderId",
                        column: x => x.CheckoutOrderId,
                        principalTable: "CheckoutOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutPaymentIntents_CheckoutOrderId",
                table: "CheckoutPaymentIntents",
                column: "CheckoutOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutPaymentIntents_Reference",
                table: "CheckoutPaymentIntents",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutPaymentIntents");
        }
    }
}
