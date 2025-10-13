using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Exports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_ExportId",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "ExportId",
                table: "Atividades");

            migrationBuilder.CreateTable(
                name: "AtividadeExport",
                columns: table => new
                {
                    AtividadeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_termino = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtividadeExport", x => x.AtividadeId);
                    table.ForeignKey(
                        name: "FK_AtividadeExport_Exports_ExportId",
                        column: x => x.ExportId,
                        principalTable: "Exports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeExport_ExportId",
                table: "AtividadeExport",
                column: "ExportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtividadeExport");

            migrationBuilder.AddColumn<Guid>(
                name: "ExportId",
                table: "Atividades",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_ExportId",
                table: "Atividades",
                column: "ExportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades",
                column: "ExportId",
                principalTable: "Exports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
