using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AlteradoEm",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlteradoPor",
                table: "Etapas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "Etapas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "CriadoPor",
                table: "Etapas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "Demandas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "CriadoPor",
                table: "Demandas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlteradoEm",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "AlteradoPor",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "CriadoPor",
                table: "Etapas");

            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "Demandas");

            migrationBuilder.DropColumn(
                name: "CriadoPor",
                table: "Demandas");
        }
    }
}
