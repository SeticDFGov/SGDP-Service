using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class Projetoentityfieldbool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projetos",
                columns: table => new
                {
                    projetoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NM_PROJETO = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GERENTE_PROJETO = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SITUACAO = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UNIDADE = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NR_PROCESSO_SEI = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NM_AREA_DEMANDANTE = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ANO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TEMPLATE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PROFISCOII = table.Column<bool>(type: "boolean", nullable: false),
                    PDTIC2427 = table.Column<bool>(type: "boolean", nullable: false),
                    PTD2427 = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projetos", x => x.projetoId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Projetos");
        }
    }
}
