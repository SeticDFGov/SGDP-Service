using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeReferenciaisRegistros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pe_arquivo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanho = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dono_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    dono_id = table.Column<long>(type: "bigint", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_arquivo", x => x.id);
                    table.CheckConstraint("ck_pe_arquivo_conteudo", "conteudo IS NOT NULL AND octet_length(conteudo) = tamanho");
                    table.CheckConstraint("ck_pe_arquivo_dono", "(dono_tipo IS NULL) = (dono_id IS NULL)");
                    table.CheckConstraint("ck_pe_arquivo_dono_tipo", "dono_tipo IS NULL OR dono_tipo IN ('registro')");
                    table.CheckConstraint("ck_pe_arquivo_tamanho", "tamanho > 0");
                });

            migrationBuilder.CreateTable(
                name: "pe_deliberacao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    objeto_tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    objeto_id = table.Column<long>(type: "bigint", nullable: false),
                    versao_objeto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    enviado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decidido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decidido_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ato_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ato_numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ato_data = table.Column<DateOnly>(type: "date", nullable: true),
                    sei = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_deliberacao", x => x.id);
                    table.CheckConstraint("ck_pe_deliberacao_aprovado", "situacao <> 'aprovado' OR (ato_numero IS NOT NULL AND ato_data IS NOT NULL)");
                    table.CheckConstraint("ck_pe_deliberacao_decisao", "(situacao = 'aguardando') = (decidido_em IS NULL) AND (decidido_em IS NULL) = (decidido_por IS NULL)");
                    table.CheckConstraint("ck_pe_deliberacao_devolvido", "situacao <> 'devolvido' OR observacao IS NOT NULL");
                    table.CheckConstraint("ck_pe_deliberacao_objeto", "objeto_tipo IN ('petic','pdtic')");
                    table.CheckConstraint("ck_pe_deliberacao_situacao", "situacao IN ('aguardando','aprovado','devolvido')");
                });

            migrationBuilder.CreateTable(
                name: "pe_petic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    versao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    anterior_id = table.Column<long>(type: "bigint", nullable: true),
                    aprovado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_petic", x => x.id);
                    table.CheckConstraint("ck_pe_petic_aprovado_em", "(situacao IN ('aprovado','substituido')) = (aprovado_em IS NOT NULL)");
                    table.CheckConstraint("ck_pe_petic_situacao", "situacao IN ('rascunho','em_deliberacao','aprovado','substituido')");
                    table.CheckConstraint("ck_pe_petic_versao", "versao ~ '^[0-9]+\\.[0-9]+$'");
                    table.CheckConstraint("ck_pe_petic_vigencia", "vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
                    table.ForeignKey(
                        name: "fk_pe_petic_anterior",
                        column: x => x.anterior_id,
                        principalTable: "pe_petic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_registro_sequencia",
                columns: table => new
                {
                    secao_id = table.Column<long>(type: "bigint", nullable: false),
                    dono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ultimo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_registro_sequencia", x => new { x.secao_id, x.dono });
                    table.CheckConstraint("ck_pe_registro_sequencia_dono", "dono ~ '^[a-z]+(:[0-9]+)?$'");
                    table.CheckConstraint("ck_pe_registro_sequencia_ultimo", "ultimo >= 0");
                    table.ForeignKey(
                        name: "fk_pe_registro_sequencia_secao",
                        column: x => x.secao_id,
                        principalTable: "pe_secao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_registro",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    secao_id = table.Column<long>(type: "bigint", nullable: false),
                    petic_id = table.Column<long>(type: "bigint", nullable: true),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    dados = table.Column<string>(type: "jsonb", nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_registro", x => x.id);
                    table.CheckConstraint("ck_pe_registro_codigo", "codigo IS NULL OR codigo ~ '^[A-Z][A-Z0-9]*[0-9]$'");
                    table.ForeignKey(
                        name: "fk_pe_registro_petic",
                        column: x => x.petic_id,
                        principalTable: "pe_petic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pe_registro_secao",
                        column: x => x.secao_id,
                        principalTable: "pe_secao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_vinculo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    registro_origem_id = table.Column<long>(type: "bigint", nullable: false),
                    campo_id = table.Column<long>(type: "bigint", nullable: false),
                    registro_destino_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_vinculo", x => x.id);
                    table.CheckConstraint("ck_pe_vinculo_origem_destino", "registro_origem_id <> registro_destino_id");
                    table.ForeignKey(
                        name: "fk_pe_vinculo_campo",
                        column: x => x.campo_id,
                        principalTable: "pe_campo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_vinculo_destino",
                        column: x => x.registro_destino_id,
                        principalTable: "pe_registro",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_pe_vinculo_origem",
                        column: x => x.registro_origem_id,
                        principalTable: "pe_registro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pe_arquivo_dono",
                table: "pe_arquivo",
                columns: new[] { "dono_tipo", "dono_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pe_deliberacao_objeto",
                table: "pe_deliberacao",
                columns: new[] { "objeto_tipo", "objeto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pe_deliberacao_situacao",
                table: "pe_deliberacao",
                columns: new[] { "situacao", "enviado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_pe_deliberacao_aguardando",
                table: "pe_deliberacao",
                columns: new[] { "objeto_tipo", "objeto_id" },
                unique: true,
                filter: "situacao = 'aguardando'");

            migrationBuilder.CreateIndex(
                name: "ix_pe_petic_anterior",
                table: "pe_petic",
                column: "anterior_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_petic_situacao",
                table: "pe_petic",
                column: "situacao",
                unique: true,
                filter: "situacao IN ('rascunho','em_deliberacao','aprovado')");

            migrationBuilder.CreateIndex(
                name: "ux_pe_petic_versao",
                table: "pe_petic",
                column: "versao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_registro_secao",
                table: "pe_registro",
                columns: new[] { "secao_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ux_pe_registro_petic_codigo",
                table: "pe_registro",
                columns: new[] { "petic_id", "secao_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_vinculo_campo",
                table: "pe_vinculo",
                column: "campo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_vinculo_destino",
                table: "pe_vinculo",
                column: "registro_destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_vinculo",
                table: "pe_vinculo",
                columns: new[] { "registro_origem_id", "campo_id", "registro_destino_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pe_arquivo");

            migrationBuilder.DropTable(
                name: "pe_deliberacao");

            migrationBuilder.DropTable(
                name: "pe_registro_sequencia");

            migrationBuilder.DropTable(
                name: "pe_vinculo");

            migrationBuilder.DropTable(
                name: "pe_registro");

            migrationBuilder.DropTable(
                name: "pe_petic");
        }
    }
}
