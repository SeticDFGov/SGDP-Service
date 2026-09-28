using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeAcompanhamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pe_doc_versao_numero",
                table: "pe_doc_versao");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_orgao_bloco",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_orgao",
                table: "pe_doc_orgao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_modelo_tipo",
                table: "pe_doc_modelo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_bloco_tipo",
                table: "pe_doc_bloco");

            migrationBuilder.AddColumn<string>(
                name: "por_ciclo",
                table: "pe_secao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ciclo_id",
                table: "pe_registro",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ciclo_id",
                table: "pe_doc_versao",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doc_tipo",
                table: "pe_doc_versao",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                defaultValue: "pdtic");

            migrationBuilder.AddColumn<long>(
                name: "ciclo_id",
                table: "pe_doc_orgao_bloco",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doc_tipo",
                table: "pe_doc_orgao_bloco",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                defaultValue: "pdtic");

            migrationBuilder.AddColumn<long>(
                name: "ciclo_id",
                table: "pe_doc_orgao",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doc_tipo",
                table: "pe_doc_orgao",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                defaultValue: "pdtic");

            migrationBuilder.CreateTable(
                name: "pe_ciclo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pdtic_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fim = table.Column<DateOnly>(type: "date", nullable: true),
                    prazo = table.Column<DateOnly>(type: "date", nullable: true),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fechado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fechado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reaberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reaberto_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_ciclo", x => x.id);
                    table.CheckConstraint("ck_pe_ciclo_fechado", "(situacao = 'fechado') = (fechado_em IS NOT NULL) AND (fechado_em IS NULL) = (fechado_por IS NULL)");
                    table.CheckConstraint("ck_pe_ciclo_numero", "numero >= 1");
                    table.CheckConstraint("ck_pe_ciclo_periodo", "(tipo <> 'monitoramento' OR (fim IS NOT NULL AND prazo IS NOT NULL)) AND (fim IS NULL OR fim >= inicio) AND (prazo IS NULL OR fim IS NULL OR prazo >= fim)");
                    table.CheckConstraint("ck_pe_ciclo_reaberto", "(reaberto_em IS NULL) = (reaberto_por IS NULL)");
                    table.CheckConstraint("ck_pe_ciclo_situacao", "situacao IN ('aberto','fechado')");
                    table.CheckConstraint("ck_pe_ciclo_tipo", "tipo IN ('monitoramento','avaliacao')");
                    table.ForeignKey(
                        name: "fk_pe_ciclo_pdtic",
                        column: x => x.pdtic_id,
                        principalTable: "pe_pdtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_secao_por_ciclo",
                table: "pe_secao",
                sql: "por_ciclo IS NULL OR por_ciclo IN ('monitoramento','avaliacao')");

            migrationBuilder.CreateIndex(
                name: "ix_pe_registro_ciclo",
                table: "pe_registro",
                column: "ciclo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_versao_ciclo",
                table: "pe_doc_versao",
                column: "ciclo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_versao_numero",
                table: "pe_doc_versao",
                columns: new[] { "pdtic_id", "numero" },
                unique: true,
                filter: "doc_tipo = 'pdtic'");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_versao_numero_rr",
                table: "pe_doc_versao",
                columns: new[] { "pdtic_id", "numero" },
                unique: true,
                filter: "doc_tipo = 'rr'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_versao_documento",
                table: "pe_doc_versao",
                sql: "doc_tipo IS NOT NULL AND doc_tipo IN ('pdtic','ra','rr') AND (doc_tipo = 'ra') = (ciclo_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_orgao_bloco_ciclo",
                table: "pe_doc_orgao_bloco",
                column: "ciclo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao_bloco",
                table: "pe_doc_orgao_bloco",
                columns: new[] { "pdtic_id", "bloco_id" },
                unique: true,
                filter: "doc_tipo <> 'ra'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_orgao_bloco_documento",
                table: "pe_doc_orgao_bloco",
                sql: "doc_tipo IS NOT NULL AND doc_tipo IN ('pdtic','ra','rr') AND (doc_tipo = 'ra') = (ciclo_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_pe_doc_orgao_ciclo",
                table: "pe_doc_orgao",
                column: "ciclo_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao",
                table: "pe_doc_orgao",
                columns: new[] { "pdtic_id", "capitulo_id" },
                unique: true,
                filter: "doc_tipo <> 'ra'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_orgao_documento",
                table: "pe_doc_orgao",
                sql: "doc_tipo IS NOT NULL AND doc_tipo IN ('pdtic','ra','rr') AND (doc_tipo = 'ra') = (ciclo_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_modelo_tipo",
                table: "pe_doc_modelo",
                sql: "tipo IN ('pdtic','ra','rr')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_bloco_tipo",
                table: "pe_doc_bloco",
                sql: "tipo IN ('texto','tabela_secao','lista_tema','matriz_swot','fluxo','quebra_pagina','acoes_por_situacao','metas_por_resultado','riscos_ocorridos','medicoes')");

            migrationBuilder.CreateIndex(
                name: "ux_pe_ciclo_avaliacao_aberta",
                table: "pe_ciclo",
                column: "pdtic_id",
                unique: true,
                filter: "tipo = 'avaliacao' AND situacao = 'aberto'");

            migrationBuilder.CreateIndex(
                name: "ux_pe_ciclo_numero",
                table: "pe_ciclo",
                columns: new[] { "pdtic_id", "tipo", "numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_pe_doc_orgao_ciclo",
                table: "pe_doc_orgao",
                column: "ciclo_id",
                principalTable: "pe_ciclo",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pe_doc_orgao_bloco_ciclo",
                table: "pe_doc_orgao_bloco",
                column: "ciclo_id",
                principalTable: "pe_ciclo",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pe_doc_versao_ciclo",
                table: "pe_doc_versao",
                column: "ciclo_id",
                principalTable: "pe_ciclo",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_pe_registro_ciclo",
                table: "pe_registro",
                column: "ciclo_id",
                principalTable: "pe_ciclo",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // O relatório de acompanhamento (RA) é um documento por ciclo: uma linha por ciclo e
            // capítulo, por ciclo e bloco, e o número da versão por ciclo. O EF não escreve estes
            // índices porque juntam colunas das duas entidades que dividem a tabela (ciclo_id é da
            // entidade à parte, capitulo_id, bloco_id e numero são da de sempre)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_pe_doc_orgao_ciclo ON pe_doc_orgao (ciclo_id, capitulo_id) WHERE ciclo_id IS NOT NULL; " +
                "CREATE UNIQUE INDEX ux_pe_doc_orgao_bloco_ciclo ON pe_doc_orgao_bloco (ciclo_id, bloco_id) WHERE ciclo_id IS NOT NULL; " +
                "CREATE UNIQUE INDEX ux_pe_doc_versao_ciclo ON pe_doc_versao (ciclo_id, numero) WHERE ciclo_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O que era só da rodada B nas tabelas que ficam (sem isso, as colunas saem e as linhas
            // do RA e do RR virariam linhas do PDTIC, e os CHECKs antigos não voltam): os PDFs, as
            // cópias e as versões do RA e do RR, os modelos ra e rr (capítulos, blocos, histórico e
            // imagens), os blocos dos tipos novos, os registros das seções por ciclo e a versão do
            // conteúdo semeado, que volta a 5 (a da rodada A), para que a rodada B, aplicada de
            // novo, carregue as seções por ciclo, as configurações e os modelos do RA e do RR
            migrationBuilder.Sql(
                "CREATE TEMP TABLE pe_rodada_b_pdfs ON COMMIT DROP AS SELECT arquivo_id FROM pe_doc_versao WHERE doc_tipo <> 'pdtic'; " +
                "DELETE FROM pe_doc_versao WHERE doc_tipo <> 'pdtic'; " +
                "DELETE FROM pe_arquivo WHERE id IN (SELECT arquivo_id FROM pe_rodada_b_pdfs); " +
                "DELETE FROM pe_doc_orgao_bloco WHERE doc_tipo <> 'pdtic'; " +
                "DELETE FROM pe_doc_orgao WHERE doc_tipo <> 'pdtic'; " +
                "DELETE FROM pe_arquivo WHERE dono_tipo = 'doc_modelo' AND dono_id IN (SELECT id FROM pe_doc_modelo WHERE tipo <> 'pdtic'); " +
                "DELETE FROM pe_modelo_historico WHERE (entidade = 'doc_bloco' AND entidade_id IN (SELECT b.id FROM pe_doc_bloco b " +
                "JOIN pe_doc_capitulo c ON c.id = b.capitulo_id JOIN pe_doc_modelo m ON m.id = c.modelo_id WHERE m.tipo <> 'pdtic' " +
                "OR b.tipo IN ('acoes_por_situacao','metas_por_resultado','riscos_ocorridos','medicoes'))) " +
                "OR (entidade = 'doc_capitulo' AND entidade_id IN (SELECT c.id FROM pe_doc_capitulo c JOIN pe_doc_modelo m ON m.id = c.modelo_id " +
                "WHERE m.tipo <> 'pdtic')); " +
                "DELETE FROM pe_doc_orgao_bloco WHERE bloco_id IN (SELECT b.id FROM pe_doc_bloco b JOIN pe_doc_capitulo c ON c.id = b.capitulo_id " +
                "JOIN pe_doc_modelo m ON m.id = c.modelo_id WHERE m.tipo <> 'pdtic'); " +
                "DELETE FROM pe_doc_bloco WHERE tipo IN ('acoes_por_situacao','metas_por_resultado','riscos_ocorridos','medicoes') " +
                "OR capitulo_id IN (SELECT c.id FROM pe_doc_capitulo c JOIN pe_doc_modelo m ON m.id = c.modelo_id WHERE m.tipo <> 'pdtic'); " +
                "DELETE FROM pe_doc_orgao WHERE capitulo_id IN (SELECT c.id FROM pe_doc_capitulo c JOIN pe_doc_modelo m ON m.id = c.modelo_id " +
                "WHERE m.tipo <> 'pdtic'); " +
                "DELETE FROM pe_doc_capitulo WHERE pai_id IS NOT NULL AND modelo_id IN (SELECT id FROM pe_doc_modelo WHERE tipo <> 'pdtic'); " +
                "DELETE FROM pe_doc_capitulo WHERE modelo_id IN (SELECT id FROM pe_doc_modelo WHERE tipo <> 'pdtic'); " +
                "DELETE FROM pe_doc_modelo WHERE tipo <> 'pdtic'; " +
                "DELETE FROM pe_registro WHERE ciclo_id IS NOT NULL; " +
                "UPDATE pe_configuracao SET valor = '5' WHERE chave = 'seed_modelo_versao' " +
                "AND CASE WHEN (valor #>> '{}') ~ '^[0-9]+$' THEN (valor #>> '{}')::int > 5 ELSE false END;");

            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS ux_pe_doc_orgao_ciclo; " +
                "DROP INDEX IF EXISTS ux_pe_doc_orgao_bloco_ciclo; " +
                "DROP INDEX IF EXISTS ux_pe_doc_versao_ciclo;");

            migrationBuilder.DropForeignKey(
                name: "fk_pe_doc_orgao_ciclo",
                table: "pe_doc_orgao");

            migrationBuilder.DropForeignKey(
                name: "fk_pe_doc_orgao_bloco_ciclo",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropForeignKey(
                name: "fk_pe_doc_versao_ciclo",
                table: "pe_doc_versao");

            migrationBuilder.DropForeignKey(
                name: "fk_pe_registro_ciclo",
                table: "pe_registro");

            migrationBuilder.DropTable(
                name: "pe_ciclo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_secao_por_ciclo",
                table: "pe_secao");

            migrationBuilder.DropIndex(
                name: "ix_pe_registro_ciclo",
                table: "pe_registro");

            migrationBuilder.DropIndex(
                name: "ix_pe_doc_versao_ciclo",
                table: "pe_doc_versao");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_versao_numero",
                table: "pe_doc_versao");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_versao_numero_rr",
                table: "pe_doc_versao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_versao_documento",
                table: "pe_doc_versao");

            migrationBuilder.DropIndex(
                name: "ix_pe_doc_orgao_bloco_ciclo",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_orgao_bloco",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_orgao_bloco_documento",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropIndex(
                name: "ix_pe_doc_orgao_ciclo",
                table: "pe_doc_orgao");

            migrationBuilder.DropIndex(
                name: "ux_pe_doc_orgao",
                table: "pe_doc_orgao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_orgao_documento",
                table: "pe_doc_orgao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_modelo_tipo",
                table: "pe_doc_modelo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pe_doc_bloco_tipo",
                table: "pe_doc_bloco");

            migrationBuilder.DropColumn(
                name: "por_ciclo",
                table: "pe_secao");

            migrationBuilder.DropColumn(
                name: "ciclo_id",
                table: "pe_registro");

            migrationBuilder.DropColumn(
                name: "ciclo_id",
                table: "pe_doc_versao");

            migrationBuilder.DropColumn(
                name: "doc_tipo",
                table: "pe_doc_versao");

            migrationBuilder.DropColumn(
                name: "ciclo_id",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropColumn(
                name: "doc_tipo",
                table: "pe_doc_orgao_bloco");

            migrationBuilder.DropColumn(
                name: "ciclo_id",
                table: "pe_doc_orgao");

            migrationBuilder.DropColumn(
                name: "doc_tipo",
                table: "pe_doc_orgao");

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_versao_numero",
                table: "pe_doc_versao",
                columns: new[] { "pdtic_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao_bloco",
                table: "pe_doc_orgao_bloco",
                columns: new[] { "pdtic_id", "bloco_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_doc_orgao",
                table: "pe_doc_orgao",
                columns: new[] { "pdtic_id", "capitulo_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_modelo_tipo",
                table: "pe_doc_modelo",
                sql: "tipo IN ('pdtic')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pe_doc_bloco_tipo",
                table: "pe_doc_bloco",
                sql: "tipo IN ('texto','tabela_secao','lista_tema','matriz_swot','fluxo','quebra_pagina')");
        }
    }
}
