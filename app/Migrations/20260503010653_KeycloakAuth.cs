using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class KeycloakAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshTokenCreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenRevokedAt",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "RefreshToken",
                table: "Users",
                newName: "KeycloakId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KeycloakId",
                table: "Users",
                newName: "RefreshToken");

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenCreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenRevokedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
