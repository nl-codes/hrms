using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkShiftWeekStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkShifts_EmployeeUserId_StartTimeUtc_EndTimeUtc",
                table: "WorkShifts");

            migrationBuilder.AddColumn<DateTime>(
                name: "WeekStartDate",
                table: "WorkShifts",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(@"
                UPDATE `WorkShifts`
                SET `WeekStartDate` = DATE_SUB(
                    DATE(`StartTimeUtc`),
                    INTERVAL ((DAYOFWEEK(DATE(`StartTimeUtc`)) + 5) % 7) DAY
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_EmployeeUserId_WeekStartDate_StartTimeUtc_EndTime~",
                table: "WorkShifts",
                columns: new[] { "EmployeeUserId", "WeekStartDate", "StartTimeUtc", "EndTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkShifts_EmployeeUserId_WeekStartDate_StartTimeUtc_EndTime~",
                table: "WorkShifts");

            migrationBuilder.DropColumn(
                name: "WeekStartDate",
                table: "WorkShifts");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_EmployeeUserId_StartTimeUtc_EndTimeUtc",
                table: "WorkShifts",
                columns: new[] { "EmployeeUserId", "StartTimeUtc", "EndTimeUtc" });
        }
    }
}
