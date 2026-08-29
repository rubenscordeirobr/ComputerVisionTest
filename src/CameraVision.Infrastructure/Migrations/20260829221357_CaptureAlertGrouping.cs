using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaptureAlertGrouping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertChannels",
                table: "Captures",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertQueuedAt",
                table: "Captures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertSentAt",
                table: "Captures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaptureAlertSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupWindowMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastDigestAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureAlertSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptureAlertSettings");

            migrationBuilder.DropColumn(
                name: "AlertChannels",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertQueuedAt",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertSentAt",
                table: "Captures");
        }
    }
}
