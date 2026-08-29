using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaptureRuleTimeWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ActiveFrom",
                table: "CaptureRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ActiveTo",
                table: "CaptureRules",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveFrom",
                table: "CaptureRules");

            migrationBuilder.DropColumn(
                name: "ActiveTo",
                table: "CaptureRules");
        }
    }
}
