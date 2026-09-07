using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PgiaFase0Fundacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PapelPgia",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pgia_agente_info",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cargo_funcao = table.Column<string>(type: "text", nullable: true),
                    vinculo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_agente_info", x => x.user_id);
                    table.CheckConstraint("ck_pgia_agente_info_vinculo", "vinculo IN ('Servidor efetivo','Comissionado','Estagiário','Prestador de serviço')");
                    table.ForeignKey(
                        name: "fk_pgia_agente_info_user",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pgia_orgao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    natureza_juridica = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    presta_servico_cidadao = table.Column<bool>(type: "boolean", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_orgao", x => x.id);
                    table.CheckConstraint("ck_pgia_orgao_natureza", "natureza_juridica IN ('Administração direta','Autarquia','Fundação pública','Empresa pública','Sociedade de economia mista')");
                    table.CheckConstraint("ck_pgia_orgao_servico_cidadao", "natureza_juridica IN ('Empresa pública','Sociedade de economia mista') OR presta_servico_cidadao IS NULL");
                    table.ForeignKey(
                        name: "fk_pgia_orgao_unidade",
                        column: x => x.unidade_id,
                        principalTable: "Unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pgia_encarregado_dados",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    agente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ato_numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ato_data = table.Column<DateOnly>(type: "date", nullable: false),
                    processo_sei_comunicacao = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    data_comunicacao_sgdi = table.Column<DateOnly>(type: "date", nullable: false),
                    inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    fim_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_encarregado_dados", x => x.id);
                    table.ForeignKey(
                        name: "fk_pgia_encarregado_agente",
                        column: x => x.agente_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_encarregado_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pgia_prazo_conformidade",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    obrigacao = table.Column<string>(type: "text", nullable: false),
                    base_legal = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    orgao_id = table.Column<long>(type: "bigint", nullable: true),
                    data_limite = table.Column<DateOnly>(type: "date", nullable: false),
                    cumprido_em = table.Column<DateOnly>(type: "date", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_prazo_conformidade", x => x.id);
                    table.ForeignKey(
                        name: "fk_pgia_prazo_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pgia_responsavel_ia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    agente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ato_numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ato_data = table.Column<DateOnly>(type: "date", nullable: false),
                    processo_sei_comunicacao = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    data_comunicacao_sgdi = table.Column<DateOnly>(type: "date", nullable: false),
                    acumula_funcao_tic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    capacitacao_adequada = table.Column<bool>(type: "boolean", nullable: true),
                    inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    fim_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pgia_responsavel_ia", x => x.id);
                    table.ForeignKey(
                        name: "fk_pgia_responsavel_ia_agente",
                        column: x => x.agente_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgia_responsavel_ia_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "pgia_prazo_conformidade",
                columns: new[] { "id", "alterado_em", "alterado_por", "base_legal", "criado_em", "criado_por", "cumprido_em", "data_limite", "obrigacao", "orgao_id" },
                values: new object[,]
                {
                    { 1L, null, null, "arts. 10, § 1º e 35, I", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 8, 2), "Designar e comunicar à SGDI o Responsável de IA", null },
                    { 2L, null, null, "art. 35, I", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 8, 2), "Designar e comunicar à SGDI o Encarregado de Dados", null },
                    { 3L, null, null, "arts. 10, II e 35, II", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 10, 1), "Elaborar e remeter o inventário inicial de sistemas de IA", null },
                    { 4L, null, null, "arts. 29, § único e 35, III", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 12, 30), "Incluir as trilhas ProCapIA/DF no plano de capacitação", null },
                    { 5L, null, null, "art. 37", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 12, 30), "Revisar e adequar atos e contratos anteriores ao decreto", null },
                    { 6L, null, null, "art. 35, IV", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2027, 7, 3), "Concluir e publicar as AIA dos sistemas de Alto Risco em operação", null },
                    { 7L, null, null, "art. 26, § 3º", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 12, 30), "SGDI: elaborar o Guia de Contratações de IA, aprovado pelo CGTIC", null },
                    { 8L, null, null, "art. 33, caput", new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2027, 3, 31), "SGDI: publicar o Relatório Anual de Governança de IA", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_encarregado_agente",
                table: "pgia_encarregado_dados",
                column: "agente_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_encarregado_vigente",
                table: "pgia_encarregado_dados",
                column: "orgao_id",
                unique: true,
                filter: "ativo");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_orgao_sigla",
                table: "pgia_orgao",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pgia_orgao_unidade",
                table: "pgia_orgao",
                column: "unidade_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pgia_prazo_orgao",
                table: "pgia_prazo_conformidade",
                columns: new[] { "orgao_id", "data_limite" });

            migrationBuilder.CreateIndex(
                name: "ix_pgia_responsavel_ia_agente",
                table: "pgia_responsavel_ia",
                column: "agente_id");

            migrationBuilder.CreateIndex(
                name: "ux_pgia_responsavel_ia_vigente",
                table: "pgia_responsavel_ia",
                column: "orgao_id",
                unique: true,
                filter: "ativo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pgia_agente_info");

            migrationBuilder.DropTable(
                name: "pgia_encarregado_dados");

            migrationBuilder.DropTable(
                name: "pgia_prazo_conformidade");

            migrationBuilder.DropTable(
                name: "pgia_responsavel_ia");

            migrationBuilder.DropTable(
                name: "pgia_orgao");

            migrationBuilder.DropColumn(
                name: "PapelPgia",
                table: "Users");
        }
    }
}
