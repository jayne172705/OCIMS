using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eSureHi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmployeeBeneficiaryContribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "receipt_number",
                table: "contributions",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "contributions",
                newName: "or_number");

            migrationBuilder.AddColumn<string>(
                name: "barangay",
                table: "vw_employee_coverage",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "barangay",
                table: "vw_claims_summary",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "contribution_date",
                table: "contributions",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "barangay",
                table: "vw_employee_coverage");

            migrationBuilder.DropColumn(
                name: "barangay",
                table: "vw_claims_summary");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "contributions",
                newName: "receipt_number");

            migrationBuilder.RenameColumn(
                name: "or_number",
                table: "contributions",
                newName: "description");

            migrationBuilder.AlterColumn<DateTime>(
                name: "contribution_date",
                table: "contributions",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }
    }
}
