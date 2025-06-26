using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class addUnidadeAndESteiraFromProjeto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EsteiraId",
                table: "Projetos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Unidadeid",
                table: "Projetos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valorEstimado",
                table: "Projetos",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Esteiras",
                columns: table => new
                {
                    EsteiraId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Esteiras", x => x.EsteiraId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projetos_EsteiraId",
                table: "Projetos",
                column: "EsteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_Projetos_Unidadeid",
                table: "Projetos",
                column: "Unidadeid");

            migrationBuilder.AddForeignKey(
                name: "FK_Projetos_Esteiras_EsteiraId",
                table: "Projetos",
                column: "EsteiraId",
                principalTable: "Esteiras",
                principalColumn: "EsteiraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projetos_Unidades_Unidadeid",
                table: "Projetos",
                column: "Unidadeid",
                principalTable: "Unidades",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projetos_Esteiras_EsteiraId",
                table: "Projetos");

            migrationBuilder.DropForeignKey(
                name: "FK_Projetos_Unidades_Unidadeid",
                table: "Projetos");

            migrationBuilder.DropTable(
                name: "Esteiras");

            migrationBuilder.DropIndex(
                name: "IX_Projetos_EsteiraId",
                table: "Projetos");

            migrationBuilder.DropIndex(
                name: "IX_Projetos_Unidadeid",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "EsteiraId",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "Unidadeid",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "valorEstimado",
                table: "Projetos");
        }
    }
}
