using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDespachoWithoutProjetoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Despachos",
                columns: table => new
                {
                    DespachoId = table.Column<Guid>(type: "uuid", nullable: false),
                    projetoId = table.Column<int>(type: "integer", nullable: false),
                    NomeOrgao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataEntrada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataSaida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despachos", x => x.DespachoId);
                    table.ForeignKey(
                        name: "FK_Despachos_Projetos_projetoId",
                        column: x => x.projetoId,
                        principalTable: "Projetos",
                        principalColumn: "projetoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Despachos_projetoId",
                table: "Despachos",
                column: "projetoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Despachos");
        }
    }
}
