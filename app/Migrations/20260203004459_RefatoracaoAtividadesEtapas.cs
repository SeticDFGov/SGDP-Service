using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class RefatoracaoAtividadesEtapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId1",
                table: "Atividades");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_EtapaProjetoId1",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "EtapaProjetoId1",
                table: "Atividades");

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_EtapaProjetoId",
                table: "Atividades",
                column: "EtapaProjetoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId",
                table: "Atividades",
                column: "EtapaProjetoId",
                principalTable: "Etapas",
                principalColumn: "EtapaProjetoId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId",
                table: "Atividades");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_EtapaProjetoId",
                table: "Atividades");

            migrationBuilder.AddColumn<int>(
                name: "EtapaProjetoId1",
                table: "Atividades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_EtapaProjetoId1",
                table: "Atividades",
                column: "EtapaProjetoId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Etapas_EtapaProjetoId1",
                table: "Atividades",
                column: "EtapaProjetoId1",
                principalTable: "Etapas",
                principalColumn: "EtapaProjetoId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
