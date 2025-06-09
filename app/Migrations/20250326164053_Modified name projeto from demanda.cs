using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Modifiednameprojetofromdemanda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Demandas",
                columns: table => new
                {
                    DemandaId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_DEMANDA = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DT_SOLICITACAO = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DT_ABERTURA = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DT_CONCLUSAO = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    STATUS = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NM_PO_SUBTDCR = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NM_PO_DEMANDANTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PATROCINADOR = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UNIDADE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NR_PROCESSO_SEI = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    PERIODICO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PERIODICIDADE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NM_AREA_DEMANDANTEAreaDemandanteID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demandas", x => x.DemandaId);
                    table.ForeignKey(
                        name: "FK_Demandas_AreaDemandantes_NM_AREA_DEMANDANTEAreaDemandanteID",
                        column: x => x.NM_AREA_DEMANDANTEAreaDemandanteID,
                        principalTable: "AreaDemandantes",
                        principalColumn: "AreaDemandanteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Demandas_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "CategoriaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_CategoriaId",
                table: "Demandas",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_NM_AREA_DEMANDANTEAreaDemandanteID",
                table: "Demandas",
                column: "NM_AREA_DEMANDANTEAreaDemandanteID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Demandas");
        }
    }
}
