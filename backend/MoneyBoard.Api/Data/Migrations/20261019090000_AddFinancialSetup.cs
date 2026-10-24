using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MoneyBoard.Api.Data;

#nullable disable

namespace MoneyBoard.Api.Data.Migrations;

[DbContext(typeof(MoneyBoardDbContext))]
[Migration("20261019090000_AddFinancialSetup")]
public partial class AddFinancialSetup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "SetupCompleted", table: "AspNetUsers", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "SetupProgress", table: "AspNetUsers", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SetupData", table: "AspNetUsers", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SetupCompleted", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "SetupProgress", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "SetupData", table: "AspNetUsers");
    }
}
