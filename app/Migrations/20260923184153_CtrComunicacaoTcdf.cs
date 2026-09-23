using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrComunicacaoTcdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ato_tcdf",
                table: "ctr_manifestacao_tcdf",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_recebimento",
                table: "ctr_manifestacao_tcdf",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_ato_tcdf",
                table: "ctr_manifestacao_tcdf",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processo_comunicacao_tcdf",
                table: "ctr_manifestacao_tcdf",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_ato_tcdf",
                table: "ctr_manifestacao_tcdf",
                sql: "ato_tcdf IS NULL OR ato_tcdf IN ('Despacho Singular','Decisão')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_comunicacao",
                table: "ctr_manifestacao_tcdf",
                sql: "(processo_comunicacao_tcdf IS NULL AND data_recebimento IS NULL AND ato_tcdf IS NULL AND numero_ato_tcdf IS NULL) OR (processo_comunicacao_tcdf IS NOT NULL AND data_recebimento IS NOT NULL AND ato_tcdf IS NOT NULL AND numero_ato_tcdf IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_ato_tcdf",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_comunicacao",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropColumn(
                name: "ato_tcdf",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropColumn(
                name: "data_recebimento",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropColumn(
                name: "numero_ato_tcdf",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropColumn(
                name: "processo_comunicacao_tcdf",
                table: "ctr_manifestacao_tcdf");
        }
    }
}
