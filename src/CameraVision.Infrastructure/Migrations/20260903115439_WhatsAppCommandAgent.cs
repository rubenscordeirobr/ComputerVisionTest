using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppCommandAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiApiKey",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiProvider",
                table: "SystemSettings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "WhatsAppBotDefaultHours",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppBotEnabled",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppWebhookSecret",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppWebhookUrl",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "http://host.docker.internal:5220/api/whatsapp/webhook");

            migrationBuilder.CreateTable(
                name: "WhatsAppCommandLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SenderJid = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SenderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PushName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    MessageAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContactId = table.Column<int>(type: "INTEGER", nullable: true),
                    Intent = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IntentSource = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TriggersAffected = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppCommandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppCommandLogs_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WhatsAppCommandLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCommandLogs_ContactId",
                table: "WhatsAppCommandLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCommandLogs_MessageId",
                table: "WhatsAppCommandLogs",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCommandLogs_SenderNumber_ReceivedAt",
                table: "WhatsAppCommandLogs",
                columns: new[] { "SenderNumber", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCommandLogs_Status_ReceivedAt",
                table: "WhatsAppCommandLogs",
                columns: new[] { "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCommandLogs_TenantId",
                table: "WhatsAppCommandLogs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppCommandLogs");

            migrationBuilder.DropColumn(
                name: "AiApiKey",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "AiProvider",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppBotDefaultHours",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppBotEnabled",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppWebhookSecret",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppWebhookUrl",
                table: "SystemSettings");
        }
    }
}
