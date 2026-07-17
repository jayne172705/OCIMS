using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eSureHi.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiarySourceOfFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_of_funds",
                table: "beneficiaries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_of_funds",
                table: "beneficiaries");
        }
    }
}
