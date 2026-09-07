using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase2Governanca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avaliacao_parecer",
                table: "pgia_sistema_ia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "avaliado_em",
                table: "pgia_sistema_ia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "avaliado_por",
                table: "pgia_sistema_ia",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "deliberacao_homologacao_id",
                table: "pgia_sistema_ia",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "situacao_homologacao",
                table: "pgia_sistema_ia",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Aguardando avaliação da SGDI");

            // Backfill: sistemas já inventariados antes da homologação seguem a rota
            // do desenho da chefia — Alto Risco e Risco Excessivo são delegados ao CGTIC,
            // não à fila da SGDI que o DEFAULT acima aplica às linhas existentes.
            migrationBuilder.Sql(
                "UPDATE pgia_sistema_ia SET situacao_homologacao = 'Aguardando deliberação do CGTIC' " +
                "WHERE classificacao_risco_atual IN ('Alto Risco','Risco Excessivo');");

            migrationBuilder.AddColumn<long>(
                name: "deliberacao_cgtic_id",
                table: "pgia_classificacao_risco",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pgia_deliberacao_cgtic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: true),
                    data_deliberacao = table.Column<DateOnly>(type: "date", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    numero_ato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ementa = table.Column<string>(type: "text", nullable: true),
                    documento_id = table.Column<long>(type: "bigint", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_deliberacao_cgtic", x => x.id);
                    table.CheckConstraint("ck_pgia_deliberacao_resultado", "resultado IN ('Favorável','Desfavorável','Em diligência')");
                    table.CheckConstraint("ck_pgia_deliberacao_tipo", "tipo IN ('Classificação de sistema como Alto Risco','Aprovação de aquisição de Alto Risco','Critérios e requisitos de aquisição','Constituição de grupo de trabalho temático','Apreciação do Relatório Anual','Resolução normativa','Autorização de treinamento com dados do GDF','Ampliação do rol de Alto Risco','Aprovação do Guia de Contratações','Suspensão de sistema')");
                    table.ForeignKey(
                        name: "fk_pgia_deliberacao_doc",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_deliberacao_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_norma_complementar",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ementa = table.Column<string>(type: "text", nullable: false),
                    emissor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    aprovada_cgtic_em = table.Column<DateOnly>(type: "date", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_norma_complementar", x => x.id);
                    table.CheckConstraint("ck_pgia_norma_emissor", "emissor IN ('SGDI','CGTIC')");
                    table.CheckConstraint("ck_pgia_norma_tipo", "tipo IN ('Resolução do CGTIC','Instrução normativa da SGDI','Norma técnica ou guia','Guia de Contratações de IA','Recomendação técnica')");
                });

            migrationBuilder.CreateTable(
                name: "pgia_plataforma_ia_generativa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "text", nullable: false),
                    fornecedor = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    status_homologacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Em avaliação"),
                    apta_dados_pessoais_sigilosos = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ato_homologacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    data_ato = table.Column<DateOnly>(type: "date", nullable: true),
                    publicada_relacao_em = table.Column<DateOnly>(type: "date", nullable: true),
                    diretrizes_uso = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_plataforma_ia_generativa", x => x.id);
                    table.CheckConstraint("ck_pgia_plataforma_status", "status_homologacao IN ('Em avaliação','Homologada','Homologada apta a dados pessoais e sigilosos','Não homologada','Homologação revogada')");
                });

            migrationBuilder.CreateTable(
                name: "pgia_aia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Não iniciada"),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    data_conclusao = table.Column<DateOnly>(type: "date", nullable: true),
                    impactos_direitos_fundamentais = table.Column<string>(type: "text", nullable: false),
                    medidas_preventivas = table.Column<string>(type: "text", nullable: false),
                    medidas_mitigadoras = table.Column<string>(type: "text", nullable: false),
                    medidas_reversao = table.Column<string>(type: "text", nullable: false),
                    previa_licitacao = table.Column<bool>(type: "boolean", nullable: true),
                    conjunta_ripd = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ripd_documento_id = table.Column<long>(type: "bigint", nullable: true),
                    elaborada_por = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<long>(type: "bigint", nullable: true),
                    deliberacao_cgtic_id = table.Column<long>(type: "bigint", nullable: true),
                    publicada_portal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_publicacao_portal = table.Column<DateOnly>(type: "date", nullable: true),
                    url_publicacao = table.Column<string>(type: "text", nullable: true),
                    proxima_revisao = table.Column<DateOnly>(type: "date", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_aia", x => x.id);
                    table.CheckConstraint("ck_pgia_aia_status", "status IN ('Não iniciada','Em elaboração','Concluída','Em revisão')");
                    table.ForeignKey(
                        name: "fk_pgia_aia_agente",
                        column: x => x.elaborada_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_aia_deliberacao",
                        column: x => x.deliberacao_cgtic_id,
                        principalTable: "pgia_deliberacao_cgtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_aia_doc",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_aia_ripd_doc",
                        column: x => x.ripd_documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_aia_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_autorizacao_excepcional",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    plataforma_id = table.Column<long>(type: "bigint", nullable: true),
                    justificativa = table.Column<string>(type: "text", nullable: false),
                    avaliacao_riscos_doc_id = table.Column<long>(type: "bigint", nullable: true),
                    autorizada_por = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    deliberacao_cgtic_id = table.Column<long>(type: "bigint", nullable: true),
                    data_autorizacao = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_autorizacao_excepcional", x => x.id);
                    table.CheckConstraint("ck_pgia_autorizacao_autorizada_por", "autorizada_por IN ('SGDI','Órgão com aprovação do CGTIC')");
                    table.CheckConstraint("ck_pgia_autorizacao_tipo", "tipo IN ('Uso de plataforma pública com dados não públicos','Uso de dados do GDF para treinamento pelo fornecedor')");
                    table.ForeignKey(
                        name: "fk_pgia_autorizacao_deliberacao",
                        column: x => x.deliberacao_cgtic_id,
                        principalTable: "pgia_deliberacao_cgtic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_autorizacao_doc",
                        column: x => x.avaliacao_riscos_doc_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_autorizacao_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_autorizacao_plataforma",
                        column: x => x.plataforma_id,
                        principalTable: "pgia_plataforma_ia_generativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_sistema_avaliador",
                table: "pgia_sistema_ia",
                column: "avaliado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_sistema_delib_homolog",
                table: "pgia_sistema_ia",
                column: "deliberacao_homologacao_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pgia_sistema_homologacao",
                table: "pgia_sistema_ia",
                sql: "situacao_homologacao IN ('Aguardando avaliação da SGDI','Aguardando deliberação do CGTIC','Aprovado','Vetado')");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_classificacao_deliberacao",
                table: "pgia_classificacao_risco",
                column: "deliberacao_cgtic_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_aia_agente",
                table: "pgia_aia",
                column: "elaborada_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_aia_deliberacao",
                table: "pgia_aia",
                column: "deliberacao_cgtic_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_aia_doc",
                table: "pgia_aia",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_aia_ripd_doc",
                table: "pgia_aia",
                column: "ripd_documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_aia_sistema",
                table: "pgia_aia",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_autorizacao_deliberacao",
                table: "pgia_autorizacao_excepcional",
                column: "deliberacao_cgtic_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_autorizacao_doc",
                table: "pgia_autorizacao_excepcional",
                column: "avaliacao_riscos_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_autorizacao_orgao",
                table: "pgia_autorizacao_excepcional",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_autorizacao_plataforma",
                table: "pgia_autorizacao_excepcional",
                column: "plataforma_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_deliberacao_doc",
                table: "pgia_deliberacao_cgtic",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_deliberacao_sistema",
                table: "pgia_deliberacao_cgtic",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_plataforma_nome",
                table: "pgia_plataforma_ia_generativa",
                column: "nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_classificacao_deliberacao",
                table: "pgia_classificacao_risco",
                column: "deliberacao_cgtic_id",
                principalTable: "pgia_deliberacao_cgtic",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_sistema_avaliador",
                table: "pgia_sistema_ia",
                column: "avaliado_por",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_sistema_deliberacao_homolog",
                table: "pgia_sistema_ia",
                column: "deliberacao_homologacao_id",
                principalTable: "pgia_deliberacao_cgtic",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pgia_classificacao_deliberacao",
                table: "pgia_classificacao_risco");

            migrationBuilder.DropForeignKey(
                name: "fk_pgia_sistema_avaliador",
                table: "pgia_sistema_ia");

            migrationBuilder.DropForeignKey(
                name: "fk_pgia_sistema_deliberacao_homolog",
                table: "pgia_sistema_ia");

            migrationBuilder.DropTable(
                name: "pgia_aia");

            migrationBuilder.DropTable(
                name: "pgia_autorizacao_excepcional");

            migrationBuilder.DropTable(
                name: "pgia_norma_complementar");

            migrationBuilder.DropTable(
                name: "pgia_deliberacao_cgtic");

            migrationBuilder.DropTable(
                name: "pgia_plataforma_ia_generativa");

            migrationBuilder.DropIndex(
                name: "ix_pgia_sistema_avaliador",
                table: "pgia_sistema_ia");

            migrationBuilder.DropIndex(
                name: "ix_pgia_sistema_delib_homolog",
                table: "pgia_sistema_ia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pgia_sistema_homologacao",
                table: "pgia_sistema_ia");

            migrationBuilder.DropIndex(
                name: "ix_pgia_classificacao_deliberacao",
                table: "pgia_classificacao_risco");

            migrationBuilder.DropColumn(
                name: "avaliacao_parecer",
                table: "pgia_sistema_ia");

            migrationBuilder.DropColumn(
                name: "avaliado_em",
                table: "pgia_sistema_ia");

            migrationBuilder.DropColumn(
                name: "avaliado_por",
                table: "pgia_sistema_ia");

            migrationBuilder.DropColumn(
                name: "deliberacao_homologacao_id",
                table: "pgia_sistema_ia");

            migrationBuilder.DropColumn(
                name: "situacao_homologacao",
                table: "pgia_sistema_ia");

            migrationBuilder.DropColumn(
                name: "deliberacao_cgtic_id",
                table: "pgia_classificacao_risco");
        }
    }
}
