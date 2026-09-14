using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrRodadaPlanejamentoTcdf : Migration
    {
        /// <inheritdoc />
        // Ordem MANUAL (o scaffold gerava o DROP da criticidade antes das colunas
        // novas, o que inviabilizaria o backfill): 1) colunas do processo,
        // 2) status_tcdf, 3) BACKFILL da criticidade, 4) CHECKs, 5) DROP da coluna.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Colunas novas de ctr_processo ──────────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "retorno_orgao_nao_se_aplica",
                table: "ctr_processo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "etapa_planejamento",
                table: "ctr_processo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_assinatura_contrato",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "criticidade",
                table: "ctr_processo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origem",
                table: "ctr_processo",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Órgão comunicante");

            // ── 2. Coluna nova de ctr_manifestacao_tcdf ───────────────────────
            migrationBuilder.AddColumn<string>(
                name: "status_tcdf",
                table: "ctr_manifestacao_tcdf",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // ── 3. BACKFILL: a criticidade sobe da manifestação para o processo ──
            // Cada processo herda a criticidade da sua manifestação MAIS RECENTE que
            // a tenha preenchida (data_oficio desc, id desc como desempate). Roda
            // ANTES do DROP da coluna, senão o dado se perderia.
            migrationBuilder.Sql(@"
                UPDATE ctr_processo p
                SET criticidade = recente.criticidade
                FROM (
                    SELECT DISTINCT ON (m.processo_id) m.processo_id, m.criticidade
                    FROM ctr_manifestacao_tcdf m
                    WHERE m.criticidade IS NOT NULL
                    ORDER BY m.processo_id, m.data_oficio DESC, m.id DESC
                ) AS recente
                WHERE recente.processo_id = p.id;
            ");

            // ── 4. CHECKs ─────────────────────────────────────────────────────
            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_criticidade",
                table: "ctr_processo",
                sql: "criticidade IS NULL OR criticidade IN ('Alta','Média','Baixa')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_etapa_planejamento",
                table: "ctr_processo",
                sql: "etapa_planejamento IS NULL OR etapa_planejamento IN ('DFD','ETP','TR')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo",
                sql: "origem IN ('Órgão comunicante','TCDF')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_retorno_orgao",
                table: "ctr_processo",
                sql: "NOT retorno_orgao_nao_se_aplica OR retorno_orgao IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_status",
                table: "ctr_manifestacao_tcdf",
                sql: "status_tcdf IS NULL OR status_tcdf IN ('Suspenso por irregularidades','Edital revogado')");

            // O CHECK dos incisos é RECRIADO sem "criticidade IS NOT NULL"
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_inciso",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_inciso",
                table: "ctr_manifestacao_tcdf",
                sql: "(situacao_portfolio = 'Comunicada previamente' AND comunicada_desde IS NOT NULL AND resultado_analise IS NOT NULL AND prazo_regularizacao_dias IS NULL) OR (situacao_portfolio = 'Não comunicada previamente' AND prazo_regularizacao_dias IS NOT NULL AND comunicada_desde IS NULL AND resultado_analise IS NULL AND desfecho_risco IS NULL AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_criticidade",
                table: "ctr_manifestacao_tcdf");

            // ── 5. A coluna sai da manifestação (o dado já subiu para o processo) ──
            migrationBuilder.DropColumn(
                name: "criticidade",
                table: "ctr_manifestacao_tcdf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_criticidade",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_etapa_planejamento",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_origem",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_retorno_orgao",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_inciso",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_manifestacao_status",
                table: "ctr_manifestacao_tcdf");

            // A coluna da manifestação volta e recebe o backfill REVERSO ANTES de
            // ctr_processo.criticidade sumir (ordem manual, como no Up)
            migrationBuilder.AddColumn<string>(
                name: "criticidade",
                table: "ctr_manifestacao_tcdf",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ctr_manifestacao_tcdf m
                SET criticidade = p.criticidade
                FROM ctr_processo p
                WHERE p.id = m.processo_id
                  AND m.situacao_portfolio = 'Comunicada previamente'
                  AND p.criticidade IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "criticidade",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "data_assinatura_contrato",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "etapa_planejamento",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "origem",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "retorno_orgao_nao_se_aplica",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "status_tcdf",
                table: "ctr_manifestacao_tcdf");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_criticidade",
                table: "ctr_manifestacao_tcdf",
                sql: "criticidade IS NULL OR criticidade IN ('Alta','Média','Baixa')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_manifestacao_inciso",
                table: "ctr_manifestacao_tcdf",
                sql: "(situacao_portfolio = 'Comunicada previamente' AND comunicada_desde IS NOT NULL AND criticidade IS NOT NULL AND resultado_analise IS NOT NULL AND prazo_regularizacao_dias IS NULL) OR (situacao_portfolio = 'Não comunicada previamente' AND prazo_regularizacao_dias IS NOT NULL AND comunicada_desde IS NULL AND criticidade IS NULL AND resultado_analise IS NULL AND desfecho_risco IS NULL AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");
        }
    }
}
