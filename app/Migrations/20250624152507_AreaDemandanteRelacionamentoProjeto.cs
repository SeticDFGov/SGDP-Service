using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class AreaDemandanteRelacionamentoProjeto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NM_AREA_DEMANDANTE",
                table: "Projetos");

            migrationBuilder.AddColumn<int>(
                name: "AreaDemandanteID",
                table: "Projetos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projetos_AreaDemandanteID",
                table: "Projetos",
                column: "AreaDemandanteID");

            migrationBuilder.AddForeignKey(
                name: "FK_Projetos_AreaDemandantes_AreaDemandanteID",
                table: "Projetos",
                column: "AreaDemandanteID",
                principalTable: "AreaDemandantes",
                principalColumn: "AreaDemandanteID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projetos_AreaDemandantes_AreaDemandanteID",
                table: "Projetos");

            migrationBuilder.DropIndex(
                name: "IX_Projetos_AreaDemandanteID",
                table: "Projetos");

            migrationBuilder.DropColumn(
                name: "AreaDemandanteID",
                table: "Projetos");

            migrationBuilder.AddColumn<string>(
                name: "NM_AREA_DEMANDANTE",
                table: "Projetos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
