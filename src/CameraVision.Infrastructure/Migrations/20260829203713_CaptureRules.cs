using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaptureRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaptureRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Classes = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceThreshold = table.Column<double>(type: "REAL", nullable: false),
                    MaxSegmentSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    LingerSeconds = table.Column<double>(type: "REAL", nullable: false),
                    NotifyEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyWhatsApp = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureRules", x => x.Id);
                });

            // Preserve the old singleton settings as the first rule; NotifyEmail follows
            // the Email channel's current master switch (rules now decide triggering).
            migrationBuilder.Sql(
                """
                INSERT INTO CaptureRules (Name, Enabled, Classes, ConfidenceThreshold,
                                          MaxSegmentSeconds, LingerSeconds, NotifyEmail,
                                          NotifyWhatsApp, CreatedAt)
                SELECT 'Regra 1', 1, cs.TrackedClasses, cs.ConfidenceThreshold,
                       cs.MaxSegmentSeconds, cs.LingerSeconds,
                       COALESCE((SELECT a.Enabled FROM AlertSettings a WHERE a.Channel = 'Email'), 0),
                       0,
                       datetime('now', 'localtime')
                FROM CaptureSettings cs;
                """);

            migrationBuilder.DropTable(
                name: "CaptureSettings");

            migrationBuilder.DropColumn(
                name: "TriggerClasses",
                table: "AlertSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaptureRules");

            migrationBuilder.AddColumn<string>(
                name: "TriggerClasses",
                table: "AlertSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CaptureSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfidenceThreshold = table.Column<double>(type: "REAL", nullable: false),
                    LingerSeconds = table.Column<double>(type: "REAL", nullable: false),
                    MaxSegmentSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackedClasses = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureSettings", x => x.Id);
                });
        }
    }
}
