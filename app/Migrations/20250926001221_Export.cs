using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Export : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExportId",
                table: "Atividades",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NM_PROJETOprojetoId = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Data_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exports_Projetos_NM_PROJETOprojetoId",
                        column: x => x.NM_PROJETOprojetoId,
                        principalTable: "Projetos",
                        principalColumn: "projetoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_ExportId",
                table: "Atividades",
                column: "ExportId");

            migrationBuilder.CreateIndex(
                name: "IX_Exports_NM_PROJETOprojetoId",
                table: "Exports",
                column: "NM_PROJETOprojetoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades",
                column: "ExportId",
                principalTable: "Exports",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Exports_ExportId",
                table: "Atividades");

            migrationBuilder.DropTable(
                name: "Exports");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_ExportId",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "ExportId",
                table: "Atividades");
        }
    }
}
