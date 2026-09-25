using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeModoLivreValidacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.AddColumn<DateTime>(
                name: "validacao_alterada_em",
                table: "pe_pdtic_passo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validado_em",
                table: "pe_pdtic_passo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validado_por",
                table: "pe_pdtic_passo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pe_orgao_passo_detalhe",
                columns: table => new
                {
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    passo_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_orgao_passo_detalhe", x => new { x.orgao_id, x.passo_id });
                    table.ForeignKey(
                        name: "fk_pe_orgao_passo_detalhe_nivel",
                        column: x => x.nivel_id,
                        principalTable: "pe_nivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_orgao_passo_detalhe_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_orgao_passo_detalhe_passo",
                        column: x => x.passo_id,
                        principalTable: "pe_passo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_passo_validacao",
                table: "pe_pdtic_passo",
                sql: "(validado_em IS NULL) = (validado_por IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_pdtic_passo_validacao_alterada",
                table: "pe_pdtic_passo",
                sql: "validacao_alterada_em IS NULL OR validado_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco','fluxo_modelo','configuracao')");

            migrationBuilder.CreateIndex(
                name: "ix_pe_orgao_passo_detalhe_nivel",
                table: "pe_orgao_passo_detalhe",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_orgao_passo_detalhe_passo",
                table: "pe_orgao_passo_detalhe",
                column: "passo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O que era só da F3 na tabela que fica (sem isso, o CHECK antigo não volta): o histórico
            // das trocas do modo dos níveis. A versão do conteúdo semeado volta a 7 (a da F1), para
            // que a F3, aplicada de novo, carregue a versão 8 outra vez. A configuração modo_niveis
            // fica (o código da F2 não a lê; com a F3 de novo, a escolha que estava nela continua)
            migrationBuilder.Sql(
                "DELETE FROM pe_modelo_historico WHERE entidade = 'configuracao'; " +
                "UPDATE pe_configuracao SET valor = '7' WHERE chave = 'seed_modelo_versao' " +
                "AND CASE WHEN (valor #>> '{}') ~ '^[0-9]+$' THEN (valor #>> '{}')::int > 7 ELSE false END;");

            migrationBuilder.DropTable(
                name: "pe_orgao_passo_detalhe");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_passo_validacao",
                table: "pe_pdtic_passo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_pdtic_passo_validacao_alterada",
                table: "pe_pdtic_passo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico");

            migrationBuilder.DropColumn(
                name: "validacao_alterada_em",
                table: "pe_pdtic_passo");

            migrationBuilder.DropColumn(
                name: "validado_em",
                table: "pe_pdtic_passo");

            migrationBuilder.DropColumn(
                name: "validado_por",
                table: "pe_pdtic_passo");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                sql: "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste','doc_capitulo','doc_bloco','fluxo_modelo')");
        }
    }
}
