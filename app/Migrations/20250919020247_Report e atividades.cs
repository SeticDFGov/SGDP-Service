using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Reporteatividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analises");

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    ReportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETOprojetoId = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Data_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_Reports_Projetos_NM_PROJETOprojetoId",
                        column: x => x.NM_PROJETOprojetoId,
                        principalTable: "Projetos",
                        principalColumn: "projetoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Atividades",
                columns: table => new
                {
                    AtividadeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_termino = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atividades", x => x.AtividadeId);
                    table.ForeignKey(
                        name: "FK_Atividades_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_ReportId",
                table: "Atividades",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_NM_PROJETOprojetoId",
                table: "Reports",
                column: "NM_PROJETOprojetoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Atividades");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.CreateTable(
                name: "Analises",
                columns: table => new
                {
                    AnaliseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETOprojetoId = table.Column<int>(type: "integer", nullable: false),
                    ANALISE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ENTRAVE = table.Column<bool>(type: "boolean", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analises", x => x.AnaliseId);
                    table.ForeignKey(
                        name: "FK_Analises_Projetos_NM_PROJETOprojetoId",
                        column: x => x.NM_PROJETOprojetoId,
                        principalTable: "Projetos",
                        principalColumn: "projetoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analises_NM_PROJETOprojetoId",
                table: "Analises",
                column: "NM_PROJETOprojetoId");
        }
    }
}
