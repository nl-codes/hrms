using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostingOpenPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpenPositions",
                table: "JobPostings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpenPositions",
                table: "JobPostings");
        }
    }
}
