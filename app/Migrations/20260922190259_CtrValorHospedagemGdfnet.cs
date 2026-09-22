using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrValorHospedagemGdfnet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hospedagem_cetic",
                table: "ctr_processo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "usa_gdfnet",
                table: "ctr_processo",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_estimado",
                table: "ctr_processo",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_hospedagem_cetic",
                table: "ctr_processo",
                sql: "hospedagem_cetic IS NULL OR hospedagem_cetic IN ('Sim','Não','Parcialmente','Não aplicável (SaaS)')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_hospedagem_cetic",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "hospedagem_cetic",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "usa_gdfnet",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "valor_estimado",
                table: "ctr_processo");
        }
    }
}
