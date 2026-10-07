using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBoard.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InspectModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DebtPayments_AspNetUsers_UserId",
                table: "DebtPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals");

            migrationBuilder.AddForeignKey(
                name: "FK_DebtPayments_AspNetUsers_UserId",
                table: "DebtPayments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DebtPayments_AspNetUsers_UserId",
                table: "DebtPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals");

            migrationBuilder.AddForeignKey(
                name: "FK_DebtPayments_AspNetUsers_UserId",
                table: "DebtPayments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavingsGoals_AspNetUsers_UserId",
                table: "SavingsGoals",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
