using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FollowUpJson",
                table: "WhatsAppCommandLogs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommandLogId = table.Column<int>(type: "INTEGER", nullable: true),
                    SenderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PushName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MessageText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Request = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSuggestions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgentSuggestions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentSuggestions_WhatsAppCommandLogs_CommandLogId",
                        column: x => x.CommandLogId,
                        principalTable: "WhatsAppCommandLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSuggestions_CommandLogId",
                table: "AgentSuggestions",
                column: "CommandLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSuggestions_ContactId",
                table: "AgentSuggestions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSuggestions_ReviewedAt_CreatedAt",
                table: "AgentSuggestions",
                columns: new[] { "ReviewedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSuggestions_TenantId",
                table: "AgentSuggestions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSuggestions");

            migrationBuilder.DropColumn(
                name: "FollowUpJson",
                table: "WhatsAppCommandLogs");
        }
    }
}
