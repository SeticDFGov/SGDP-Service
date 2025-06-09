using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Etapa1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Etapas",
                columns: table => new
                {
                    EtapaProjetoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETOprojetoId = table.Column<int>(type: "integer", nullable: false),
                    NM_ETAPA = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DT_INICIO_PREVISTO = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DT_TERMINO_PREVISTO = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DT_INICIO_REAL = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DT_TERMINO_REAL = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SITUACAO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RESPONSAVEL_ETAPA = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ANALISE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PERCENT_TOTAL_ETAPA = table.Column<decimal>(type: "numeric", nullable: false),
                    PERCENT_EXEC_ETAPA = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etapas", x => x.EtapaProjetoId);
                    table.ForeignKey(
                        name: "FK_Etapas_Projetos_NM_PROJETOprojetoId",
                        column: x => x.NM_PROJETOprojetoId,
                        principalTable: "Projetos",
                        principalColumn: "projetoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Etapas_NM_PROJETOprojetoId",
                table: "Etapas",
                column: "NM_PROJETOprojetoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Etapas");
        }
    }
}
