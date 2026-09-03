using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppAudioCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioMimeType",
                table: "WhatsAppCommandLogs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioPath",
                table: "WhatsAppCommandLogs",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioSeconds",
                table: "WhatsAppCommandLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "WhatsAppCommandLogs",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppAudioEnabled",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "WhatsAppAudioMaxSeconds",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "WhisperApiKey",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "cameravision-whisper-key");

            migrationBuilder.AddColumn<string>(
                name: "WhisperBaseUrl",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "http://localhost:9000");

            migrationBuilder.AddColumn<string>(
                name: "WhisperLanguage",
                table: "SystemSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "pt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioMimeType",
                table: "WhatsAppCommandLogs");

            migrationBuilder.DropColumn(
                name: "AudioPath",
                table: "WhatsAppCommandLogs");

            migrationBuilder.DropColumn(
                name: "AudioSeconds",
                table: "WhatsAppCommandLogs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "WhatsAppCommandLogs");

            migrationBuilder.DropColumn(
                name: "WhatsAppAudioEnabled",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppAudioMaxSeconds",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhisperApiKey",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhisperBaseUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhisperLanguage",
                table: "SystemSettings");
        }
    }
}
