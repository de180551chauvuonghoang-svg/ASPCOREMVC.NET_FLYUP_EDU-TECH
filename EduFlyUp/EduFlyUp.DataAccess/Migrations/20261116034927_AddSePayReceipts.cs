using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduFlyUp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSePayReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bổ sung bảng của main cho SQL Server mới, giữ nguyên bảng đã tạo thủ công.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'Certificates', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Certificates] (
                        [Id] int NOT NULL IDENTITY,
                        [CertificateCode] nvarchar(50) NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [CourseId] int NOT NULL,
                        [StudentName] nvarchar(250) NOT NULL,
                        [CourseTitle] nvarchar(500) NOT NULL,
                        [FinalScore] float NOT NULL,
                        [IssuedAt] datetime2 NOT NULL,
                        [VerificationUrl] nvarchar(max) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Certificates_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_Certificates_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_Certificates_CertificateCode] ON [Certificates] ([CertificateCode]);
                    CREATE INDEX [IX_Certificates_CourseId] ON [Certificates] ([CourseId]);
                    CREATE INDEX [IX_Certificates_UserId] ON [Certificates] ([UserId]);
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'Orders', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Orders] (
                        [Id] int NOT NULL IDENTITY,
                        [OrderCode] nvarchar(50) NOT NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [CourseId] int NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [Status] int NOT NULL,
                        [PaymentMethod] nvarchar(50) NOT NULL,
                        [TransactionReference] nvarchar(100) NULL,
                        [PaidAt] datetime2 NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Orders_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_Orders_CourseId] ON [Orders] ([CourseId]);
                    CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
                END;
                """);

            migrationBuilder.CreateTable(
                name: "SePayReceipts",
                columns: table => new
                {
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CourseTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstructorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SePayReceipts", x => x.TransactionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SePayReceipts_InstructorId",
                table: "SePayReceipts",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_SePayReceipts_OrderId",
                table: "SePayReceipts",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Orders và Certificates thuộc main, có thể đã chứa dữ liệu trước migration.
            migrationBuilder.DropTable(
                name: "SePayReceipts");
        }
    }
}
