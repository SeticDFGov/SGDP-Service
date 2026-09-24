using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace demanda_service.Migrations
{
    /// <summary>
    /// Ajustes da SGDI de 2026-09-24 no módulo Supervisão Contínua das Contratações: Nº SEI do
    /// Formulário, documento SEI do pedido de esclarecimentos e análise técnica (seis colunas
    /// nullable e três CHECKs, só em ctr_processo) e a criticidade de TODOS os processos com
    /// respostas aos critérios recalculada pela regra nova (critério II novo, III invertido).
    /// Nada é apagado: as respostas gravadas (inclusive as da pergunta anterior do II, que
    /// passam a contar como Desconhecido) ficam como estão; só a coluna derivada criticidade
    /// é regravada. O Down recalcula pela regra anterior antes de tirar as colunas.
    /// </summary>
    public partial class CtrCriticidadeFormularioAnaliseTecnica : Migration
    {
        /// <summary>
        /// Regra de 2026-09-24 (espelho, só para esta regravação, de CtrCriticidade, a fonte
        /// única): I Sim -1; II Sim 0 e qualquer outra (Não, Desconhecido, sem resposta ou a
        /// resposta à pergunta anterior) 1; III Sim 0 e qualquer outra (Não ou sem resposta) 1;
        /// IV e VI de 0 a 3 pelo grau (o "Não foi possível avaliar" do IV vale 0); V Sim 3; soma
        /// nunca negativa; VII Sim é Alta; Baixa até 2, Média de 3 a 5, Alta com 6 ou mais.
        /// </summary>
        internal const string RecalcularCriticidadeRegraNova = """
            UPDATE ctr_processo AS p
            SET criticidade = CASE
                    WHEN c.valor_no_limite THEN 'Alta'
                    WHEN c.pontos >= 6 THEN 'Alta'
                    WHEN c.pontos >= 3 THEN 'Média'
                    ELSE 'Baixa'
                END
            FROM (
                SELECT id,
                    COALESCE(criticidade_criterios ->> 'VII', '') = 'Sim' AS valor_no_limite,
                    GREATEST(0,
                          (CASE WHEN criticidade_criterios ->> 'I' = 'Sim' THEN -1 ELSE 0 END)
                        + (CASE WHEN criticidade_criterios ->> 'II' = 'Sim' THEN 0 ELSE 1 END)
                        + (CASE WHEN criticidade_criterios ->> 'III' = 'Sim' THEN 0 ELSE 1 END)
                        + (CASE criticidade_criterios ->> 'IV' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)
                        + (CASE WHEN criticidade_criterios ->> 'V' = 'Sim' THEN 3 ELSE 0 END)
                        + (CASE criticidade_criterios ->> 'VI' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)
                    ) AS pontos
                FROM ctr_processo
                WHERE criticidade_criterios IS NOT NULL AND jsonb_typeof(criticidade_criterios) = 'object'
            ) AS c
            WHERE p.id = c.id;
            """;

        /// <summary>
        /// Regra anterior (2026-09-22), para o Down: II de 0 a 3 pelo grau (as respostas da
        /// pergunta nova contavam zero, como qualquer resposta fora do domínio) e III Sim 1.
        /// </summary>
        internal const string RecalcularCriticidadeRegraAnterior = """
            UPDATE ctr_processo AS p
            SET criticidade = CASE
                    WHEN c.valor_no_limite THEN 'Alta'
                    WHEN c.pontos >= 6 THEN 'Alta'
                    WHEN c.pontos >= 3 THEN 'Média'
                    ELSE 'Baixa'
                END
            FROM (
                SELECT id,
                    COALESCE(criticidade_criterios ->> 'VII', '') = 'Sim' AS valor_no_limite,
                    GREATEST(0,
                          (CASE WHEN criticidade_criterios ->> 'I' = 'Sim' THEN -1 ELSE 0 END)
                        + (CASE criticidade_criterios ->> 'II' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)
                        + (CASE WHEN criticidade_criterios ->> 'III' = 'Sim' THEN 1 ELSE 0 END)
                        + (CASE criticidade_criterios ->> 'IV' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)
                        + (CASE WHEN criticidade_criterios ->> 'V' = 'Sim' THEN 3 ELSE 0 END)
                        + (CASE criticidade_criterios ->> 'VI' WHEN 'Baixo' THEN 1 WHEN 'Médio' THEN 2 WHEN 'Alto' THEN 3 ELSE 0 END)
                    ) AS pontos
                FROM ctr_processo
                WHERE criticidade_criterios IS NOT NULL AND jsonb_typeof(criticidade_criterios) = 'object'
            ) AS c
            WHERE p.id = c.id;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "analise_tecnica_area",
                table: "ctr_processo",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "analise_tecnica_encaminhada_em",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "analise_tecnica_retorno_em",
                table: "ctr_processo",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analise_tecnica_retorno_resumo",
                table: "ctr_processo",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "esclarecimento_documento_sei",
                table: "ctr_processo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_sei_formulario",
                table: "ctr_processo",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_analise_tecnica",
                table: "ctr_processo",
                sql: "(analise_tecnica_encaminhada_em IS NULL AND analise_tecnica_area IS NULL AND analise_tecnica_retorno_em IS NULL AND analise_tecnica_retorno_resumo IS NULL) OR (analise_tecnica_encaminhada_em IS NOT NULL AND analise_tecnica_area IS NOT NULL AND (analise_tecnica_retorno_em IS NULL OR analise_tecnica_retorno_em >= analise_tecnica_encaminhada_em) AND (analise_tecnica_retorno_resumo IS NULL OR analise_tecnica_retorno_em IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_analise_tecnica_area",
                table: "ctr_processo",
                sql: "analise_tecnica_area IS NULL OR analise_tecnica_area IN ('SUBSIS','SUBINFRA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ctr_processo_esclarecimento_documento",
                table: "ctr_processo",
                sql: "esclarecimento_documento_sei IS NULL OR esclarecimento_solicitado_em IS NOT NULL");

            // Regra nova para todos (escrito à mão: o scaffold só gera o esquema). Regrava só a
            // coluna derivada; processo sem respostas guarda a criticidade de antes da regra
            migrationBuilder.Sql(RecalcularCriticidadeRegraNova);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta a criticidade derivada para a regra anterior, que o código anterior usa
            migrationBuilder.Sql(RecalcularCriticidadeRegraAnterior);

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_analise_tecnica",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_analise_tecnica_area",
                table: "ctr_processo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ctr_processo_esclarecimento_documento",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "analise_tecnica_area",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "analise_tecnica_encaminhada_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "analise_tecnica_retorno_em",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "analise_tecnica_retorno_resumo",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "esclarecimento_documento_sei",
                table: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "numero_sei_formulario",
                table: "ctr_processo");
        }
    }
}
