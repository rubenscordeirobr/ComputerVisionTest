using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HealthAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraHealthEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CameraId = table.Column<int>(type: "INTEGER", nullable: true),
                    CameraName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Suppressed = table.Column<bool>(type: "INTEGER", nullable: false),
                    DigestedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraHealthEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraHealthEvents_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HealthAlertSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyWhatsApp = table.Column<bool>(type: "INTEGER", nullable: false),
                    WeakLatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ConsecutiveChecks = table.Column<int>(type: "INTEGER", nullable: false),
                    NotifyRecovery = table.Column<bool>(type: "INTEGER", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FloodCapCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FloodCapWindowMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DigestEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DigestIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastDigestAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthAlertSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraHealthEvents_CameraId_OccurredAt",
                table: "CameraHealthEvents",
                columns: new[] { "CameraId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CameraHealthEvents_OccurredAt",
                table: "CameraHealthEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraHealthEvents");

            migrationBuilder.DropTable(
                name: "HealthAlertSettings");
        }
    }
}
