using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrListaSituacoesHistoricos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo");

            migrationBuilder.AddColumn<string>(
                name: "autorizacao_documento_sei",
                table: "ctr_processo",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nota_informativa_documento_sei",
                table: "ctr_processo",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "nota_informativa_enviada_em",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_contrato",
                table: "ctr_processo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_anterior_regime",
                table: "ctr_processo",
                sql: "origem <> 'Anterior ao regime' OR (data_assinatura_contrato IS NOT NULL AND numero_contrato IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_nota_informativa",
                table: "ctr_processo",
                sql: "nota_informativa_documento_sei IS NULL OR nota_informativa_enviada_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo",
                sql: "origem IN ('Órgão comunicante','TCDF','Anterior ao regime')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_anterior_regime",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_nota_informativa",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "autorizacao_documento_sei",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "nota_informativa_documento_sei",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "nota_informativa_enviada_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "numero_contrato",
                table: "ctr_processo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo",
                sql: "origem IN ('Órgão comunicante','TCDF')");
        }
    }
}
