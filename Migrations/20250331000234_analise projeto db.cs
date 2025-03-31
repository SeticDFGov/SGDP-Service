using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class analiseprojetodb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analises");
        }
    }
}
