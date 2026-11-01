using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduFlyUp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutSubmissionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionKey",
                table: "PayoutRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequests_SubmissionKey",
                table: "PayoutRequests",
                column: "SubmissionKey",
                unique: true,
                filter: "[SubmissionKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayoutRequests_SubmissionKey",
                table: "PayoutRequests");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                table: "PayoutRequests");
        }
    }
}
