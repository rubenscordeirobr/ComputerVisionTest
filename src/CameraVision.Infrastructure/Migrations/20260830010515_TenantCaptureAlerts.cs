using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <summary>
    /// Capture-alert antiflood settings become per-tenant (the old global
    /// singleton row is handed to the first tenant) and every alert delivery
    /// attempt is recorded in the new CaptureAlertLogs table. Captures learn
    /// which rule matched (AlertRuleId) so the grouped digest can attribute
    /// its log rows.
    /// </summary>
    public partial class TenantCaptureAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertRuleId",
                table: "Captures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CaptureAlertSettings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CaptureAlertSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // The old global singleton row becomes the first tenant's settings.
            // Rows that cannot be assigned (no tenants yet) are dropped — the
            // unique TenantId index and the Tenants FK below require valid rows;
            // DbInitializer reseeds fresh databases.
            migrationBuilder.Sql(
                """
                DELETE FROM CaptureAlertSettings WHERE Id NOT IN (SELECT MIN(Id) FROM CaptureAlertSettings);
                UPDATE CaptureAlertSettings SET TenantId = (SELECT MIN(Id) FROM Tenants) WHERE EXISTS (SELECT 1 FROM Tenants);
                DELETE FROM CaptureAlertSettings WHERE TenantId = 0;
                """);

            migrationBuilder.CreateTable(
                name: "CaptureAlertLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaptureId = table.Column<int>(type: "INTEGER", nullable: false),
                    CaptureRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureAlertLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptureAlertLogs_CaptureRules_CaptureRuleId",
                        column: x => x.CaptureRuleId,
                        principalTable: "CaptureRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaptureAlertLogs_Captures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Captures_AlertRuleId",
                table: "Captures",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertSettings_TenantId",
                table: "CaptureAlertSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertLogs_CaptureId",
                table: "CaptureAlertLogs",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertLogs_CaptureRuleId",
                table: "CaptureAlertLogs",
                column: "CaptureRuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaptureAlertSettings_Tenants_TenantId",
                table: "CaptureAlertSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Captures_CaptureRules_AlertRuleId",
                table: "Captures",
                column: "AlertRuleId",
                principalTable: "CaptureRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaptureAlertSettings_Tenants_TenantId",
                table: "CaptureAlertSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Captures_CaptureRules_AlertRuleId",
                table: "Captures");

            migrationBuilder.DropTable(
                name: "CaptureAlertLogs");

            migrationBuilder.DropIndex(
                name: "IX_Captures_AlertRuleId",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_CaptureAlertSettings_TenantId",
                table: "CaptureAlertSettings");

            migrationBuilder.DropColumn(
                name: "AlertRuleId",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CaptureAlertSettings");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CaptureAlertSettings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);
        }
    }
}
