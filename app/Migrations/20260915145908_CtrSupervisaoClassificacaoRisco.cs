using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrSupervisaoClassificacaoRisco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "pendencias_tcdf",
                table: "ctr_manifestacao_tcdf",
                newName: "esclarecimentos_adicionais");

            migrationBuilder.AddColumn<string>(
                name: "checklist_risco",
                table: "ctr_processo",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enquadramento_risco",
                table: "ctr_processo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pontuacao_risco",
                table: "ctr_processo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risco_classificado",
                table: "ctr_processo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "risco_classificado_em",
                table: "ctr_processo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risco_classificado_por",
                table: "ctr_processo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ctr_risco_declarado",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    processo_id = table.Column<long>(type: "bigint", nullable: false),
                    descricao_risco = table.Column<string>(type: "text", nullable: false),
                    acao_mitigacao = table.Column<string>(type: "text", nullable: false),
                    responsavel_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    responsavel_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    probabilidade = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    consequencia = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ctr_risco_declarado", x => x.id);
                    table.CheckConstraint("ck_ctr_risco_declarado_consequencia", "consequencia IN ('Desprezível','Menor','Moderada','Maior','Catastrófica')");
                    table.CheckConstraint("ck_ctr_risco_declarado_probabilidade", "probabilidade IN ('Improvável','Raro','Possível','Provável','Quase certo')");
                    table.ForeignKey(
                        name: "fk_ctr_risco_declarado_processo",
                        column: x => x.processo_id,
                        principalTable: "ctr_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_classificacao",
                table: "ctr_processo",
                sql: "(checklist_risco IS NULL AND risco_classificado IS NULL AND pontuacao_risco IS NULL AND risco_classificado_em IS NULL) OR (checklist_risco IS NOT NULL AND risco_classificado IS NOT NULL AND pontuacao_risco IS NOT NULL AND risco_classificado_em IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_risco_classificado",
                table: "ctr_processo",
                sql: "risco_classificado IS NULL OR risco_classificado IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')");

            migrationBuilder.CreateIndex(
                name: "ix_ctr_risco_declarado_processo",
                table: "ctr_risco_declarado",
                column: "processo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ctr_risco_declarado");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_classificacao",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_risco_classificado",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "checklist_risco",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "enquadramento_risco",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "pontuacao_risco",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "risco_classificado",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "risco_classificado_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "risco_classificado_por",
                table: "ctr_processo");

            migrationBuilder.RenameColumn(
                name: "esclarecimentos_adicionais",
                table: "ctr_manifestacao_tcdf",
                newName: "pendencias_tcdf");
        }
    }
}
