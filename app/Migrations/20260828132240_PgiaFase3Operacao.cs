using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase3Operacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pgia_capacitacao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    trilha = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Prevista"),
                    data_conclusao = table.Column<DateOnly>(type: "date", nullable: true),
                    prevista_plano_capacitacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    certificado_doc_id = table.Column<long>(type: "bigint", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_capacitacao", x => x.id);
                    table.CheckConstraint("ck_pgia_capacitacao_status", "status IN ('Prevista','Em andamento','Concluída')");
                    table.CheckConstraint("ck_pgia_capacitacao_trilha", "trilha IN ('Letramento em IA','Uso responsável de IA','Governança de IA','Desenvolvimento e auditoria de sistemas de IA')");
                    table.ForeignKey(
                        name: "fk_pgia_capacitacao_agente",
                        column: x => x.agente_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_capacitacao_doc",
                        column: x => x.certificado_doc_id,
                        principalTable: "pgia_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_capacitacao_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_incidente",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    hipotese = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    data_ocorrencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_deteccao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notificado_responsavel_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    comunicado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    data_comunicacao_sgdi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    status_apuracao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Recebida"),
                    recomendacoes_sgdi = table.Column<string>(type: "text", nullable: true),
                    proposta_cgtic = table.Column<bool>(type: "boolean", nullable: true),
                    encaminhado_orgao_competente_em = table.Column<DateOnly>(type: "date", nullable: true),
                    sistema_suspenso = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_suspensao = table.Column<DateOnly>(type: "date", nullable: true),
                    medidas_adotadas = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_incidente", x => x.id);
                    table.CheckConstraint("ck_pgia_incidente_hipotese", "hipotese IN ('I','II','III','IV','V')");
                    table.CheckConstraint("ck_pgia_incidente_status", "status_apuracao IN ('Recebida','Em apuração','Concluída com recomendações','Concluída sem medidas','Encaminhada ao CGTIC')");
                    table.ForeignKey(
                        name: "fk_pgia_incidente_agente",
                        column: x => x.comunicado_por,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_incidente_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_incidente_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_nao_conformidade",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    data_registro = table.Column<DateOnly>(type: "date", nullable: false),
                    reportada_sgdi_em = table.Column<DateOnly>(type: "date", nullable: true),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Registrada"),
                    comunicada_controle_interno_em = table.Column<DateOnly>(type: "date", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_nao_conformidade", x => x.id);
                    table.CheckConstraint("ck_pgia_nc_origem", "origem IN ('Acompanhamento do SGTIC','Supervisão da SGDI')");
                    table.CheckConstraint("ck_pgia_nc_situacao", "situacao IN ('Registrada','Reportada à SGDI','Em tratamento','Sanada','Comunicada ao controle interno')");
                    table.ForeignKey(
                        name: "fk_pgia_nc_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_registro_uso_ia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    sistema_ia_id = table.Column<long>(type: "bigint", nullable: true),
                    plataforma_id = table.Column<long>(type: "bigint", nullable: true),
                    produto_ref = table.Column<string>(type: "text", nullable: false),
                    processo_sei = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    data_uso = table.Column<DateOnly>(type: "date", nullable: false),
                    revisao_humana_confirmada = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_registro_uso_ia", x => x.id);
                    table.CheckConstraint("ck_pgia_uso_fonte", "num_nonnulls(sistema_ia_id, plataforma_id) = 1");
                    table.ForeignKey(
                        name: "fk_pgia_uso_agente",
                        column: x => x.agente_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_uso_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_uso_plataforma",
                        column: x => x.plataforma_id,
                        principalTable: "pgia_plataforma_ia_generativa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_uso_sistema",
                        column: x => x.sistema_ia_id,
                        principalTable: "pgia_sistema_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_capacitacao_doc",
                table: "pgia_capacitacao",
                column: "certificado_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_capacitacao_orgao",
                table: "pgia_capacitacao",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_capacitacao",
                table: "pgia_capacitacao",
                columns: new[] { "agente_id", "trilha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pgia_incidente_agente",
                table: "pgia_incidente",
                column: "comunicado_por");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_incidente_orgao",
                table: "pgia_incidente",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_incidente_sistema",
                table: "pgia_incidente",
                column: "sistema_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_nc_orgao",
                table: "pgia_nao_conformidade",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_uso_agente",
                table: "pgia_registro_uso_ia",
                column: "agente_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_uso_orgao",
                table: "pgia_registro_uso_ia",
                column: "orgao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_uso_plataforma",
                table: "pgia_registro_uso_ia",
                column: "plataforma_id");

            migrationBuilder.CreateIndex(
                name: "ix_pgia_uso_sistema",
                table: "pgia_registro_uso_ia",
                column: "sistema_ia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_capacitacao");

            migrationBuilder.DropTable(
                name: "pgia_incidente");

            migrationBuilder.DropTable(
                name: "pgia_nao_conformidade");

            migrationBuilder.DropTable(
                name: "pgia_registro_uso_ia");
        }
    }
}
