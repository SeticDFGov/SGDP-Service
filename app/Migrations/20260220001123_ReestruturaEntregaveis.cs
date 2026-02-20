using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class ReestruturaEntregaveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AreaDemandantes",
                columns: table => new
                {
                    AreaDemandanteID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_DEMANDANTE = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NM_SIGLA = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaDemandantes", x => x.AreaDemandanteID);
                });

            migrationBuilder.CreateTable(
                name: "AreasExecutoras",
                columns: table => new
                {
                    AreaExecutoraId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreasExecutoras", x => x.AreaExecutoraId);
                });

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

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Demandas",
                columns: table => new
                {
                    demandaId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETO = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NR_PROCESSO_SEI = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AREA_DEMANDANTEAreaDemandanteID = table.Column<int>(type: "integer", nullable: true),
                    EsteiraId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demandas", x => x.demandaId);
                    table.ForeignKey(
                        name: "FK_Demandas_AreaDemandantes_AREA_DEMANDANTEAreaDemandanteID",
                        column: x => x.AREA_DEMANDANTEAreaDemandanteID,
                        principalTable: "AreaDemandantes",
                        principalColumn: "AreaDemandanteID");
                    table.ForeignKey(
                        name: "FK_Demandas_Esteiras_EsteiraId",
                        column: x => x.EsteiraId,
                        principalTable: "Esteiras",
                        principalColumn: "EsteiraId");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Perfil = table.Column<string>(type: "text", nullable: false),
                    Unidadeid = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Unidades_Unidadeid",
                        column: x => x.Unidadeid,
                        principalTable: "Unidades",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Etapas",
                columns: table => new
                {
                    EtapaProjetoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETOdemandaId = table.Column<int>(type: "integer", nullable: false),
                    NM_ETAPA = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponsavelAreaExecutoraId = table.Column<int>(type: "integer", nullable: true),
                    PERCENT_EXECUTADO = table.Column<decimal>(type: "numeric", nullable: false),
                    TIPO_ENTREGA = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DT_INICIO = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DT_FIM = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etapas", x => x.EtapaProjetoId);
                    table.ForeignKey(
                        name: "FK_Etapas_AreasExecutoras_ResponsavelAreaExecutoraId",
                        column: x => x.ResponsavelAreaExecutoraId,
                        principalTable: "AreasExecutoras",
                        principalColumn: "AreaExecutoraId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Etapas_Demandas_NM_PROJETOdemandaId",
                        column: x => x.NM_PROJETOdemandaId,
                        principalTable: "Demandas",
                        principalColumn: "demandaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_AREA_DEMANDANTEAreaDemandanteID",
                table: "Demandas",
                column: "AREA_DEMANDANTEAreaDemandanteID");

            migrationBuilder.CreateIndex(
                name: "IX_Demandas_EsteiraId",
                table: "Demandas",
                column: "EsteiraId");

            migrationBuilder.CreateIndex(
                name: "IX_Etapas_NM_PROJETOdemandaId",
                table: "Etapas",
                column: "NM_PROJETOdemandaId");

            migrationBuilder.CreateIndex(
                name: "IX_Etapas_ResponsavelAreaExecutoraId",
                table: "Etapas",
                column: "ResponsavelAreaExecutoraId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Unidadeid",
                table: "Users",
                column: "Unidadeid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Etapas");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AreasExecutoras");

            migrationBuilder.DropTable(
                name: "Demandas");

            migrationBuilder.DropTable(
                name: "Unidades");

            migrationBuilder.DropTable(
                name: "AreaDemandantes");

            migrationBuilder.DropTable(
                name: "Esteiras");
        }
    }
}
