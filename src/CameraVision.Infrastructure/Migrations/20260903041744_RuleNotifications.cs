using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <summary>
    /// Contacts, per-rule notification triggers and the delivery outbox (SPEC-16).
    /// Hand-ordered: the new tables and the GroupWindowMinutes column are created
    /// first, the data is copied with raw SQL, and only then the old structures are
    /// dropped — the SQLite provider defers table rebuilds (dropped columns) to the
    /// end of the migration, so anything that reads the old columns must run before.
    /// Migrated data: each tenant recipient becomes a Contact (flagged for health
    /// alerts); NotifyEmail/NotifyWhatsApp become one "Always" trigger per rule and
    /// channel over those contacts; the tenant antiflood window is copied to every
    /// rule of the tenant; CaptureAlertLogs rows become AlertDeliveries (recipient
    /// unknown). Captures still queued at upgrade time are not converted.
    /// </summary>
    public partial class RuleNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. New structures — nothing here depends on the old columns.
            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    NotifyCameraHealth = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaptureRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ContactIds = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Days = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    ActiveFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertTriggers_CaptureRules_CaptureRuleId",
                        column: x => x.CaptureRuleId,
                        principalTable: "CaptureRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    CaptureId = table.Column<int>(type: "INTEGER", nullable: false),
                    CaptureRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ContactId = table.Column<int>(type: "INTEGER", nullable: true),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_CaptureRules_CaptureRuleId",
                        column: x => x.CaptureRuleId,
                        principalTable: "CaptureRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_Captures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "Captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlertDeliveries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_CaptureId",
                table: "AlertDeliveries",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_CaptureRuleId_SentAt",
                table: "AlertDeliveries",
                columns: new[] { "CaptureRuleId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_ContactId",
                table: "AlertDeliveries",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_Status_QueuedAt",
                table: "AlertDeliveries",
                columns: new[] { "Status", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertDeliveries_TenantId",
                table: "AlertDeliveries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertTriggers_CaptureRuleId",
                table: "AlertTriggers",
                column: "CaptureRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_TenantId_Name",
                table: "Contacts",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // Added before any Drop* on CaptureRules so it is emitted as a plain
            // ALTER TABLE ADD COLUMN (a pending rebuild would defer it past the SQL below).
            migrationBuilder.AddColumn<int>(
                name: "GroupWindowMinutes",
                table: "CaptureRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            // 2. Data copy — reads the old columns, so it must precede the drops.
            migrationBuilder.Sql(
                """
                INSERT INTO Contacts (TenantId, Name, Email, WhatsAppNumber, NotifyCameraHealth, CreatedAt)
                SELECT s.TenantId, j.value, j.value, NULL, 1, strftime('%Y-%m-%d %H:%M:%S', 'now', 'localtime')
                  FROM AlertSettings s, json_each(s.Recipients) j
                 WHERE s.Channel = 'Email';

                INSERT INTO Contacts (TenantId, Name, Email, WhatsAppNumber, NotifyCameraHealth, CreatedAt)
                SELECT s.TenantId, j.value, NULL, j.value, 1, strftime('%Y-%m-%d %H:%M:%S', 'now', 'localtime')
                  FROM AlertSettings s, json_each(s.Recipients) j
                 WHERE s.Channel = 'WhatsApp';

                INSERT INTO AlertTriggers (CaptureRuleId, Enabled, Channel, ContactIds, Kind, Days, StartTime, EndTime, ActiveFrom, ExpiresAt, CreatedAt)
                SELECT r.Id, 1, 'Email',
                       COALESCE((SELECT json_group_array(c.Id) FROM Contacts c WHERE c.TenantId = r.TenantId AND c.Email IS NOT NULL), '[]'),
                       'Always', 127, NULL, NULL, NULL, NULL, r.CreatedAt
                  FROM CaptureRules r
                 WHERE r.NotifyEmail = 1;

                INSERT INTO AlertTriggers (CaptureRuleId, Enabled, Channel, ContactIds, Kind, Days, StartTime, EndTime, ActiveFrom, ExpiresAt, CreatedAt)
                SELECT r.Id, 1, 'WhatsApp',
                       COALESCE((SELECT json_group_array(c.Id) FROM Contacts c WHERE c.TenantId = r.TenantId AND c.WhatsAppNumber IS NOT NULL), '[]'),
                       'Always', 127, NULL, NULL, NULL, NULL, r.CreatedAt
                  FROM CaptureRules r
                 WHERE r.NotifyWhatsApp = 1;

                UPDATE CaptureRules
                   SET GroupWindowMinutes = COALESCE(
                       (SELECT CASE WHEN s.GroupingEnabled = 1 THEN s.GroupWindowMinutes ELSE 0 END
                          FROM CaptureAlertSettings s
                         WHERE s.TenantId = CaptureRules.TenantId), 3);

                INSERT INTO AlertDeliveries (TenantId, CaptureId, CaptureRuleId, Channel, ContactId, Recipient, QueuedAt, SentAt, Status, ErrorMessage)
                SELECT c.TenantId, l.CaptureId, l.CaptureRuleId,
                       CASE l.Channel WHEN 0 THEN 'Email' ELSE 'WhatsApp' END,
                       NULL, NULL, l.SentAt, l.SentAt,
                       CASE l.Status WHEN 0 THEN 'Sent' ELSE 'Failed' END,
                       l.ErrorMessage
                  FROM CaptureAlertLogs l
                  JOIN Captures c ON c.Id = l.CaptureId
                 ORDER BY l.Id;
                """);

            // 3. Old structures (the column drops become table rebuilds at the end).
            migrationBuilder.DropTable(
                name: "CaptureAlertLogs");

            migrationBuilder.DropTable(
                name: "CaptureAlertSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Captures_CaptureRules_AlertRuleId",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_Captures_AlertRuleId",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertChannels",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertQueuedAt",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertRuleId",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "AlertSentAt",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "NotifyEmail",
                table: "CaptureRules");

            migrationBuilder.DropColumn(
                name: "NotifyWhatsApp",
                table: "CaptureRules");

            migrationBuilder.DropColumn(
                name: "Recipients",
                table: "AlertSettings");
        }

        /// <inheritdoc />
        /// <remarks>Structural reverse only — the migrated data is not restored.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertDeliveries");

            migrationBuilder.DropTable(
                name: "AlertTriggers");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropColumn(
                name: "GroupWindowMinutes",
                table: "CaptureRules");

            migrationBuilder.AddColumn<bool>(
                name: "NotifyEmail",
                table: "CaptureRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyWhatsApp",
                table: "CaptureRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.AddColumn<int>(
                name: "AlertRuleId",
                table: "Captures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertSentAt",
                table: "Captures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipients",
                table: "AlertSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "CaptureAlertLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaptureId = table.Column<int>(type: "INTEGER", nullable: false),
                    CaptureRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "CaptureAlertSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupWindowMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastDigestAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureAlertSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaptureAlertSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Captures_AlertRuleId",
                table: "Captures",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertLogs_CaptureId",
                table: "CaptureAlertLogs",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertLogs_CaptureRuleId",
                table: "CaptureAlertLogs",
                column: "CaptureRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureAlertSettings_TenantId",
                table: "CaptureAlertSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Captures_CaptureRules_AlertRuleId",
                table: "Captures",
                column: "AlertRuleId",
                principalTable: "CaptureRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
