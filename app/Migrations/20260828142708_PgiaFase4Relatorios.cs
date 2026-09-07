using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase4Relatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "contrato_id",
                table: "pgia_deliberacao_cgtic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "contrato_id",
                table: "pgia_autorizacao_excepcional",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pgia_auditoria_tecnica",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entidade_auditora = table.Column<string>(type: "text", nullable: false),
                    auditor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    externa_fornecedor = table.Column<bool>(type: "boolean", nullable: false),
                    apoio_fapdf = table.Column<bool>(type: "boolean", nullable: true),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    data_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    parecer = table.Column<string>(type: "text", nullable: true),
                    publicado_portal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_auditoria_tecnica", x => x.id);
                    table.CheckConstraint("ck_pgia_auditoria_tipo", "tipo IN ('Periódica','Independente contratual')");
                    table.ForeignKey(
                        name: "fk_pgia_auditoria_auditor",
                        column: x => x.auditor_user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_auditoria_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_contrato_ia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: true),
                    numero_contrato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    objeto = table.Column<string>(type: "text", nullable: false),
                    fornecedor_nome = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Vigente"),
                    previsto_pdtic = table.Column<bool>(type: "boolean", nullable: true),
                    homologacao_sgdi_doc_id = table.Column<long>(type: "bigint", nullable: true),
                    clausula_vedacao_treinamento = table.Column<bool>(type: "boolean", nullable: false),
                    autorizacao_treinamento_id = table.Column<long>(type: "bigint", nullable: true),
                    req_explicabilidade = table.Column<bool>(type: "boolean", nullable: false),
                    req_auditabilidade = table.Column<bool>(type: "boolean", nullable: false),
                    req_portabilidade = table.Column<bool>(type: "boolean", nullable: false),
                    req_sem_aprisionamento = table.Column<bool>(type: "boolean", nullable: false),
                    req_acessibilidade = table.Column<bool>(type: "boolean", nullable: false),
                    clausula_auditoria_independente = table.Column<bool>(type: "boolean", nullable: true),
                    sla_desempenho = table.Column<decimal>(type: "numeric", nullable: true),
                    sla_acuracia = table.Column<decimal>(type: "numeric", nullable: true),
                    sla_equidade = table.Column<decimal>(type: "numeric", nullable: true),
                    sla_disponibilidade = table.Column<decimal>(type: "numeric", nullable: true),
                    sla_penalidades = table.Column<string>(type: "text", nullable: true),
                    conforme_guia_contratacoes = table.Column<bool>(type: "boolean", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_contrato_ia", x => x.id);
                    table.CheckConstraint("ck_pgia_contrato_status", "status IN ('Vigente','Suspenso','Encerrado')");
                    table.ForeignKey(
                        name: "fk_pgia_contrato_autorizacao",
                        column: x => x.autorizacao_treinamento_id,
                        principalTable: "pgia_autorizacao_excepcional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_contrato_homologacao_doc",
                        column: x => x.homologacao_sgdi_doc_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_contrato_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_contrato_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_indicador_desempenho",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valor = table.Column<decimal>(type: "numeric", nullable: false),
                    unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    periodo_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    meta = table.Column<decimal>(type: "numeric", nullable: true),
                    publicado_registro_publico = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_indicador_desempenho", x => x.id);
                    table.CheckConstraint("ck_pgia_indicador_categoria", "categoria IN ('Desempenho','Adoção','Conformidade','Impacto','Acurácia','Equidade','Disponibilidade')");
                    table.CheckConstraint("ck_pgia_indicador_periodo", "periodo_fim >= periodo_inicio");
                    table.ForeignKey(
                        name: "fk_pgia_indicador_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_instrumento_legado",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    envolve_ia = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Incerto"),
                    data_triagem = table.Column<DateOnly>(type: "date", nullable: true),
                    triado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    revisado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_revisao = table.Column<DateOnly>(type: "date", nullable: true),
                    aditivo_clausula_treinamento = table.Column<bool>(type: "boolean", nullable: true),
                    data_aditivo = table.Column<DateOnly>(type: "date", nullable: true),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    comprovacao_doc_id = table.Column<long>(type: "bigint", nullable: true),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_instrumento_legado", x => x.id);
                    table.CheckConstraint("ck_pgia_legado_envolve_ia", "envolve_ia IN ('Sim','Não','Incerto')");
                    table.CheckConstraint("ck_pgia_legado_tipo", "tipo_instrumento IN ('Contrato','Ato normativo','Convênio ou instrumento congênere')");
                    table.ForeignKey(
                        name: "fk_pgia_legado_doc",
                        column: x => x.comprovacao_doc_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_legado_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_legado_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_legado_triador",
                        column: x => x.triado_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_relatorio_anual",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ano = table.Column<short>(type: "smallint", nullable: false),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: true),
                    url_publicacao = table.Column<string>(type: "text", nullable: true),
                    documento_id = table.Column<long>(type: "bigint", nullable: true),
                    apreciado_cgtic_em = table.Column<DateOnly>(type: "date", nullable: true),
                    recomendacoes = table.Column<string>(type: "text", nullable: true),
                    agenda_inovacao = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_relatorio_anual", x => x.id);
                    table.ForeignKey(
                        name: "fk_pgia_relatorio_anual_doc",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_relatorio_semestral",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    ano = table.Column<short>(type: "smallint", nullable: false),
                    semestre = table.Column<short>(type: "smallint", nullable: false),
                    data_envio = table.Column<DateOnly>(type: "date", nullable: true),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    documento_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pendente"),
                    registrado_painel_em = table.Column<DateOnly>(type: "date", nullable: true),
                    comunicado_controle_interno_em = table.Column<DateOnly>(type: "date", nullable: true),
                    prazo_envio = table.Column<DateOnly>(type: "date", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_relatorio_semestral", x => x.id);
                    table.CheckConstraint("ck_pgia_relatorio_semestre", "semestre IN (1, 2)");
                    table.CheckConstraint("ck_pgia_relatorio_status", "status IN ('Pendente','Enviado no prazo','Enviado em atraso','Inadimplente')");
                    table.ForeignKey(
                        name: "fk_pgia_relatorio_semestral_doc",
                        column: x => x.documento_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_relatorio_semestral_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_deliberacao_contrato",
                table: "pgia_deliberacao_cgtic",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_autorizacao_contrato",
                table: "pgia_autorizacao_excepcional",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_auditoria_auditor",
                table: "pgia_auditoria_tecnica",
                column: "auditor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_auditoria_sistema",
                table: "pgia_auditoria_tecnica",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_contrato_autorizacao",
                table: "pgia_contrato_ia",
                column: "autorizacao_treinamento_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_contrato_homologacao_doc",
                table: "pgia_contrato_ia",
                column: "homologacao_sgdi_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_contrato_orgao",
                table: "pgia_contrato_ia",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_contrato_sistema",
                table: "pgia_contrato_ia",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_indicador_sistema",
                table: "pgia_indicador_desempenho",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_legado_doc",
                table: "pgia_instrumento_legado",
                column: "comprovacao_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_legado_orgao",
                table: "pgia_instrumento_legado",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_legado_sistema",
                table: "pgia_instrumento_legado",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_legado_triador",
                table: "pgia_instrumento_legado",
                column: "triado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_relatorio_anual_doc",
                table: "pgia_relatorio_anual",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_relatorio_anual_ano",
                table: "pgia_relatorio_anual",
                column: "ano",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pgia_relatorio_semestral_doc",
                table: "pgia_relatorio_semestral",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_relatorio_semestral",
                table: "pgia_relatorio_semestral",
                columns: new[] { "orgao_id", "ano", "semestre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_autorizacao_contrato",
                table: "pgia_autorizacao_excepcional",
                column: "contrato_id",
                principalTable: "pgia_contrato_ia",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pgia_deliberacao_contrato",
                table: "pgia_deliberacao_cgtic",
                column: "contrato_id",
                principalTable: "pgia_contrato_ia",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pgia_autorizacao_contrato",
                table: "pgia_autorizacao_excepcional");

            migrationBuilder.DropForeignKey(
                name: "fk_pgia_deliberacao_contrato",
                table: "pgia_deliberacao_cgtic");

            migrationBuilder.DropTable(
                name: "pgia_auditoria_tecnica");

            migrationBuilder.DropTable(
                name: "pgia_contrato_ia");

            migrationBuilder.DropTable(
                name: "pgia_indicador_desempenho");

            migrationBuilder.DropTable(
                name: "pgia_instrumento_legado");

            migrationBuilder.DropTable(
                name: "pgia_relatorio_anual");

            migrationBuilder.DropTable(
                name: "pgia_relatorio_semestral");

            migrationBuilder.DropIndex(
                name: "ix_pgia_deliberacao_contrato",
                table: "pgia_deliberacao_cgtic");

            migrationBuilder.DropIndex(
                name: "ix_pgia_autorizacao_contrato",
                table: "pgia_autorizacao_excepcional");

            migrationBuilder.DropColumn(
                name: "contrato_id",
                table: "pgia_deliberacao_cgtic");

            migrationBuilder.DropColumn(
                name: "contrato_id",
                table: "pgia_autorizacao_excepcional");
        }
    }
}
