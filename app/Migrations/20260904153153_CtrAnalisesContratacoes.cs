using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class CtrAnalisesContratacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PapelContratacoes",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ctr_processo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_processo = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    orgao_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orgao_sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    complemento_area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    objeto = table.Column<string>(type: "text", nullable: false),
                    categoria_objeto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    chegada_sgdi = table.Column<DateOnly>(type: "date", nullable: true),
                    chegada_subgd = table.Column<DateOnly>(type: "date", nullable: true),
                    chegada_ugtic = table.Column<DateOnly>(type: "date", nullable: true),
                    ugtic_nao_se_aplica = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    retorno_gab_sgdi = table.Column<DateOnly>(type: "date", nullable: true),
                    retorno_orgao = table.Column<DateOnly>(type: "date", nullable: true),
                    restituido = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    restituido_em = table.Column<DateOnly>(type: "date", nullable: true),
                    restituido_motivo = table.Column<string>(type: "text", nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ctr_processo", x => x.id);
                    table.CheckConstraint("ck_ctr_processo_categoria", "categoria_objeto IN ('Infraestrutura de Rede','Desenvolvimento / Fábrica de Software','Outsourcing de Impressão','Licenciamento de Software','Inteligência Artificial','Segurança Cibernética','Sistemas de Gestão','Certificação Digital','Captação Audiovisual','Telefonia / VoIP','Sem objeto / Indefinido','Outros')");
                    table.CheckConstraint("ck_ctr_processo_restituicao", "NOT restituido OR (restituido_em IS NOT NULL AND restituido_motivo IS NOT NULL)");
                    table.CheckConstraint("ck_ctr_processo_ugtic", "NOT ugtic_nao_se_aplica OR chegada_ugtic IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "ctr_manifestacao_tcdf",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    processo_id = table.Column<long>(type: "bigint", nullable: false),
                    oficio_tcdf = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    data_oficio = table.Column<DateOnly>(type: "date", nullable: false),
                    situacao_portfolio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    comunicada_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    criticidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    resultado_analise = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    recomendou_suspensao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    comunicou_controle_interno = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    desfecho_risco = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    prazo_regularizacao_dias = table.Column<int>(type: "integer", nullable: true),
                    observacao = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ctr_manifestacao_tcdf", x => x.id);
                    table.CheckConstraint("ck_ctr_manifestacao_criticidade", "criticidade IS NULL OR criticidade IN ('Alta','Média','Baixa')");
                    table.CheckConstraint("ck_ctr_manifestacao_desfecho", "desfecho_risco IS NULL OR desfecho_risco IN ('Aguardando resposta','Risco resolvido','Não pode prosseguir')");
                    table.CheckConstraint("ck_ctr_manifestacao_inciso", "(situacao_portfolio = 'Comunicada previamente' AND comunicada_desde IS NOT NULL AND criticidade IS NOT NULL AND resultado_analise IS NOT NULL AND prazo_regularizacao_dias IS NULL) OR (situacao_portfolio = 'Não comunicada previamente' AND prazo_regularizacao_dias IS NOT NULL AND comunicada_desde IS NULL AND criticidade IS NULL AND resultado_analise IS NULL AND desfecho_risco IS NULL AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");
                    table.CheckConstraint("ck_ctr_manifestacao_prazo", "prazo_regularizacao_dias IS NULL OR prazo_regularizacao_dias > 0");
                    table.CheckConstraint("ck_ctr_manifestacao_resultado", "resultado_analise IS NULL OR resultado_analise IN ('Alinhada','Informações complementares','Riscos significativos')");
                    table.CheckConstraint("ck_ctr_manifestacao_risco", "(resultado_analise = 'Riscos significativos' AND desfecho_risco IS NOT NULL) OR (resultado_analise IS DISTINCT FROM 'Riscos significativos' AND desfecho_risco IS NULL AND NOT recomendou_suspensao AND NOT comunicou_controle_interno)");
                    table.CheckConstraint("ck_ctr_manifestacao_situacao", "situacao_portfolio IN ('Comunicada previamente','Não comunicada previamente')");
                    table.ForeignKey(
                        name: "fk_ctr_manifestacao_processo",
                        column: x => x.processo_id,
                        principalTable: "ctr_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ctr_manifestacao_processo",
                table: "ctr_manifestacao_tcdf",
                column: "processo_id");

            migrationBuilder.CreateIndex(
                name: "ix_ctr_processo_chegada_sgdi",
                table: "ctr_processo",
                column: "chegada_sgdi");

            migrationBuilder.CreateIndex(
                name: "ix_ctr_processo_sigla",
                table: "ctr_processo",
                column: "orgao_sigla");

            migrationBuilder.CreateIndex(
                name: "ux_ctr_processo_numero",
                table: "ctr_processo",
                column: "numero_processo",
                unique: true,
                filter: "ativo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ctr_manifestacao_tcdf");

            migrationBuilder.DropTable(
                name: "ctr_processo");

            migrationBuilder.DropColumn(
                name: "PapelContratacoes",
                table: "Users");
        }
    }
}
