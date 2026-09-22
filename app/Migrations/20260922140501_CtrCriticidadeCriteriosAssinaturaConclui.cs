using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrCriticidadeCriteriosAssinaturaConclui : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "criticidade_criterios",
                table: "ctr_processo",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "criticidade_criterios",
                table: "ctr_processo");
        }
    }
}
