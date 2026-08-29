using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CameraProcessorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredStream",
                table: "Cameras",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProcessorStatus",
                table: "Cameras",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessorStatusAt",
                table: "Cameras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubStreamUrl",
                table: "Cameras",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredStream",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ProcessorStatus",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ProcessorStatusAt",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "SubStreamUrl",
                table: "Cameras");
        }
    }
}
