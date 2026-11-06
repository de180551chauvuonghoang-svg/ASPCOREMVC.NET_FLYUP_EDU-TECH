using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduFlyUp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutOrdersAndPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BuyerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutOrders", x => x.Id);
                    table.CheckConstraint("CK_CheckoutOrders_Amounts", "[OriginalAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [OriginalAmount] AND [PayableAmount] = [OriginalAmount] - [DiscountAmount]");
                    table.CheckConstraint("CK_CheckoutOrders_Currency", "[Currency] = 'VND'");
                    table.CheckConstraint("CK_CheckoutOrders_Keys", "[SubmissionKey] <> '00000000-0000-0000-0000-000000000000' AND [PublicId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_CheckoutOrders_Status", "[Status] IN (0,1,2,3) AND (([Status] = 1 AND [PaidAtUtc] IS NOT NULL) OR ([Status] <> 1 AND [PaidAtUtc] IS NULL))");
                });

            migrationBuilder.CreateTable(
                name: "CheckoutOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckoutOrderId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CourseTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstructorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CouponId = table.Column<int>(type: "int", nullable: true),
                    CouponCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CouponPercent = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutOrderItems", x => x.Id);
                    table.CheckConstraint("CK_CheckoutOrderItems_Amounts", "[OriginalAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [OriginalAmount] AND [PayableAmount] = [OriginalAmount] - [DiscountAmount]");
                    table.CheckConstraint("CK_CheckoutOrderItems_Coupon", "([CouponId] IS NULL AND [CouponCode] IS NULL AND [CouponPercent] IS NULL AND [DiscountAmount] = 0) OR ([CouponId] IS NOT NULL AND [CouponId] > 0 AND [CouponCode] IS NOT NULL AND [CouponPercent] IS NOT NULL AND [CouponPercent] BETWEEN 1 AND 90)");
                    table.CheckConstraint("CK_CheckoutOrderItems_Course", "[CourseId] > 0");
                    table.ForeignKey(
                        name: "FK_CheckoutOrderItems_CheckoutOrders_CheckoutOrderId",
                        column: x => x.CheckoutOrderId,
                        principalTable: "CheckoutOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckoutOrderId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.CheckConstraint("CK_PaymentTransactions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PaymentTransactions_Currency", "[Currency] = 'VND'");
                    table.CheckConstraint("CK_PaymentTransactions_Reference", "LEN(LTRIM(RTRIM([Provider]))) > 0 AND LEN(LTRIM(RTRIM([ProviderTransactionId]))) > 0");
                    table.CheckConstraint("CK_PaymentTransactions_Status", "[Status] IN (0,1,2) AND (([Status] = 1 AND [VerifiedAtUtc] IS NOT NULL) OR ([Status] <> 1 AND [VerifiedAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_CheckoutOrders_CheckoutOrderId",
                        column: x => x.CheckoutOrderId,
                        principalTable: "CheckoutOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutOrderItems_CheckoutOrderId_CourseId",
                table: "CheckoutOrderItems",
                columns: new[] { "CheckoutOrderId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutOrderItems_InstructorId",
                table: "CheckoutOrderItems",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutOrders_BuyerId_CreatedAt",
                table: "CheckoutOrders",
                columns: new[] { "BuyerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutOrders_BuyerId_SubmissionKey",
                table: "CheckoutOrders",
                columns: new[] { "BuyerId", "SubmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutOrders_PublicId",
                table: "CheckoutOrders",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CheckoutOrderId",
                table: "PaymentTransactions",
                column: "CheckoutOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderTransactionId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutOrderItems");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "CheckoutOrders");
        }
    }
}
