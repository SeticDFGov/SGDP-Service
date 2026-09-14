using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrEsclarecimentosPendencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "esclarecimento_descricao",
                table: "ctr_processo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "esclarecimento_respondido_em",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "esclarecimento_solicitado_em",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pendencias_tcdf",
                table: "ctr_manifestacao_tcdf",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_esclarecimento",
                table: "ctr_processo",
                sql: "(esclarecimento_solicitado_em IS NULL AND esclarecimento_descricao IS NULL AND esclarecimento_respondido_em IS NULL) OR (esclarecimento_solicitado_em IS NOT NULL AND esclarecimento_descricao IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_esclarecimento",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "esclarecimento_descricao",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "esclarecimento_respondido_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "esclarecimento_solicitado_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "pendencias_tcdf",
                table: "ctr_manifestacao_tcdf");
        }
    }
}
