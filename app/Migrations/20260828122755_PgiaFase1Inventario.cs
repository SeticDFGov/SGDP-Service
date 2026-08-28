using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase1Inventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pgia_sistema_ia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    denominacao = table.Column<string>(type: "text", nullable: false),
                    finalidade = table.Column<string>(type: "text", nullable: false),
                    origem_registro = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem_registro_descricao = table.Column<string>(type: "text", nullable: true),
                    tipo_sistema = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tecnologia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status_ciclo_vida = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    data_implantacao = table.Column<DateOnly>(type: "date", nullable: true),
                    data_descontinuacao = table.Column<DateOnly>(type: "date", nullable: true),
                    escopo_dados = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    afeta_cidadao = table.Column<bool>(type: "boolean", nullable: false),
                    natureza_decisoes = table.Column<string>(type: "text", nullable: true),
                    efeitos_cidadao = table.Column<string>(type: "text", nullable: true),
                    classificacao_risco_atual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    enquadramento_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    supervisao_humana_descricao = table.Column<string>(type: "text", nullable: true),
                    aviso_interacao_ia = table.Column<bool>(type: "boolean", nullable: true),
                    identificador_autenticidade = table.Column<bool>(type: "boolean", nullable: true),
                    base_legal_lgpd = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    categorias_dados_pessoais = table.Column<string>(type: "text", nullable: true),
                    finalidade_tratamento_dados = table.Column<string>(type: "text", nullable: true),
                    medidas_seguranca = table.Column<string>(type: "text", nullable: true),
                    interoperavel_padroes_sgdi = table.Column<bool>(type: "boolean", nullable: false),
                    justificativa_nao_redundancia = table.Column<string>(type: "text", nullable: true),
                    data_analise_sgtic = table.Column<DateOnly>(type: "date", nullable: true),
                    parecer_sgtic = table.Column<string>(type: "text", nullable: true),
                    responsavel_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    comunicado_sgdi_em = table.Column<DateOnly>(type: "date", nullable: true),
                    publicado_registro_publico = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_publicacao_registro = table.Column<DateOnly>(type: "date", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_sistema_ia", x => x.id);
                    table.CheckConstraint("ck_pgia_sistema_base_legal", "base_legal_lgpd IN ('Execução de políticas públicas','Cumprimento de obrigação legal ou regulatória','Consentimento do titular','Proteção da vida','Tutela da saúde','Exercício regular de direitos','Outra hipótese dos arts. 7º e 11 da LGPD')");
                    table.CheckConstraint("ck_pgia_sistema_escopo", "escopo_dados IN ('Somente dados públicos','Dados pessoais','Dados pessoais sensíveis','Dados sigilosos')");
                    table.CheckConstraint("ck_pgia_sistema_origem", "origem_registro IN ('Nova iniciativa','Instrumento vigente em revisão','Outros')");
                    table.CheckConstraint("ck_pgia_sistema_origem_outros", "origem_registro <> 'Outros' OR origem_registro_descricao IS NOT NULL");
                    table.CheckConstraint("ck_pgia_sistema_risco", "classificacao_risco_atual IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')");
                    table.CheckConstraint("ck_pgia_sistema_status", "status_ciclo_vida IN ('Concepção','Planejamento','Em aquisição','Desenvolvimento','Treinamento','Testagem','Validação','Implantado (em uso)','Monitoramento','Descontinuado')");
                    table.CheckConstraint("ck_pgia_sistema_tecnologia", "tecnologia IN ('IA generativa','IA preditiva ou aprendizado de máquina','Visão computacional','Processamento de linguagem natural','Biometria','Outra')");
                    table.CheckConstraint("ck_pgia_sistema_tipo", "tipo_sistema IN ('Desenvolvido internamente','Contratado','Embarcado em solução adquirida','Plataforma pública de IA generativa')");
                    table.ForeignKey(
                        name: "fk_pgia_sistema_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_sistema_responsavel",
                        column: x => x.responsavel_ia_id,
                        principalTable: "pgia_responsavel_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_classificacao_risco",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    data_classificacao = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    respostas_checklist = table.Column<string>(type: "jsonb", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pontuacao_risco = table.Column<int>(type: "integer", nullable: false),
                    enquadramento_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    justificativa = table.Column<string>(type: "text", nullable: false),
                    classificado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_classificacao_risco", x => x.id);
                    table.CheckConstraint("ck_pgia_classificacao_enquadramento", "resultado = 'Baixo Risco' OR enquadramento_legal IS NOT NULL");
                    table.CheckConstraint("ck_pgia_classificacao_motivo", "motivo IN ('Classificação inicial','Nova contratação','Revisão de instrumento vigente','Reclassificação por resolução do CGTIC','Revisão periódica')");
                    table.CheckConstraint("ck_pgia_classificacao_resultado", "resultado IN ('Risco Excessivo','Alto Risco','Risco Moderado','Baixo Risco')");
                    table.ForeignKey(
                        name: "fk_pgia_classificacao_agente",
                        column: x => x.classificado_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_classificacao_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_documento",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: true),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    nome_arquivo = table.Column<string>(type: "text", nullable: false),
                    url_storage = table.Column<string>(type: "text", nullable: true),
                    data_envio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    enviado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_documento", x => x.id);
                    table.CheckConstraint("ck_pgia_documento_tipo", "tipo IN ('Ato de designação','AIA','RIPD','Ata ou deliberação','Relatório semestral','Relatório anual','Certificado de capacitação','Contrato','Termo aditivo','Parecer de auditoria','Avaliação de riscos','Comprovação de triagem','Outro')");
                    table.ForeignKey(
                        name: "fk_pgia_documento_agente",
                        column: x => x.enviado_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_documento_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_documento_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_classificacao_agente",
                table: "pgia_classificacao_risco",
                column: "classificado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_classificacao_sistema",
                table: "pgia_classificacao_risco",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_documento_agente",
                table: "pgia_documento",
                column: "enviado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_documento_orgao",
                table: "pgia_documento",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_documento_sistema",
                table: "pgia_documento",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_sistema_classificacao",
                table: "pgia_sistema_ia",
                column: "classificacao_risco_atual");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_sistema_orgao",
                table: "pgia_sistema_ia",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_sistema_responsavel",
                table: "pgia_sistema_ia",
                column: "responsavel_ia_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_sistema_orgao_nome",
                table: "pgia_sistema_ia",
                columns: new[] { "orgao_id", "denominacao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_classificacao_risco");

            migrationBuilder.DropTable(
                name: "pgia_documento");

            migrationBuilder.DropTable(
                name: "pgia_sistema_ia");
        }
    }
}
