using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CameraVision.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-14: tenants table + TenantId scoping + roles. Existing installations
    /// (detected by having users) get tenant 1 "Rubens Cordeiro" owning every
    /// pre-existing row; the admin account becomes a tenant-less SuperAdmin.
    /// Fresh databases skip the backfill — DbInitializer seeds them instead.
    /// </summary>
    public partial class MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);

            // Existing installation (has users) → seed the default tenant that will
            // own every pre-existing row. Fresh databases skip this (no users yet).
            migrationBuilder.Sql(
                """
                INSERT INTO Tenants (Id, Name, IsActive, CreatedAt)
                SELECT 1, 'Rubens Cordeiro', 1, datetime('now', 'localtime')
                WHERE EXISTS (SELECT 1 FROM Users);
                """);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Captures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CaptureRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Cameras",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CameraHealthEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AlertSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: every pre-existing row belongs to the default tenant, the
            // IsAdmin flag maps to the tenant-admin role, and the seeded admin
            // account becomes the (tenant-less) SuperAdmin. No-ops on fresh DBs.
            migrationBuilder.Sql(
                """
                UPDATE Users SET Role = CASE WHEN IsAdmin = 1 THEN 'Admin' ELSE 'User' END, TenantId = 1;
                UPDATE Users SET Role = 'SuperAdmin', TenantId = NULL WHERE LOWER(Username) = 'admin';
                UPDATE Cameras SET TenantId = 1;
                UPDATE CaptureRules SET TenantId = 1;
                UPDATE Captures SET TenantId = 1;
                UPDATE CameraHealthEvents SET TenantId = 1;
                UPDATE AlertSettings SET TenantId = 1;
                """);

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_AlertSettings_Channel",
                table: "AlertSettings");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_TenantId",
                table: "Captures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureRules_TenantId",
                table: "CaptureRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_TenantId",
                table: "Cameras",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CameraHealthEvents_TenantId",
                table: "CameraHealthEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertSettings_TenantId_Channel",
                table: "AlertSettings",
                columns: new[] { "TenantId", "Channel" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertSettings_Tenants_TenantId",
                table: "AlertSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CameraHealthEvents_Tenants_TenantId",
                table: "CameraHealthEvents",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cameras_Tenants_TenantId",
                table: "Cameras",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaptureRules_Tenants_TenantId",
                table: "CaptureRules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Captures_Tenants_TenantId",
                table: "Captures",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertSettings_Tenants_TenantId",
                table: "AlertSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_CameraHealthEvents_Tenants_TenantId",
                table: "CameraHealthEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Cameras_Tenants_TenantId",
                table: "Cameras");

            migrationBuilder.DropForeignKey(
                name: "FK_CaptureRules_Tenants_TenantId",
                table: "CaptureRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Captures_Tenants_TenantId",
                table: "Captures");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Captures_TenantId",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_CaptureRules_TenantId",
                table: "CaptureRules");

            migrationBuilder.DropIndex(
                name: "IX_Cameras_TenantId",
                table: "Cameras");

            migrationBuilder.DropIndex(
                name: "IX_CameraHealthEvents_TenantId",
                table: "CameraHealthEvents");

            migrationBuilder.DropIndex(
                name: "IX_AlertSettings_TenantId_Channel",
                table: "AlertSettings");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE Users SET IsAdmin = CASE WHEN Role IN ('Admin', 'SuperAdmin') THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Captures");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CaptureRules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CameraHealthEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AlertSettings");

            migrationBuilder.CreateIndex(
                name: "IX_AlertSettings_Channel",
                table: "AlertSettings",
                column: "Channel",
                unique: true);
        }
    }
}
