using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkerMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAlertSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyWhatsApp = table.Column<bool>(type: "INTEGER", nullable: false),
                    Emails = table.Column<string>(type: "TEXT", nullable: false),
                    WhatsAppNumbers = table.Column<string>(type: "TEXT", nullable: false),
                    WorkerDownAfterSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NotifyRecovery = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlertSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAlertEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAlertEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Device = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ActiveCameras = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerStatus", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAlertEvents_OccurredAt",
                table: "SystemAlertEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAlertSettings");

            migrationBuilder.DropTable(
                name: "SystemAlertEvents");

            migrationBuilder.DropTable(
                name: "WorkerStatus");
        }
    }
}
