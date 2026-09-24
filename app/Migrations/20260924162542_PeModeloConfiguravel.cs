using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace demanda_service.Migrations
{
    /// <inheritdoc />
    public partial class PeModeloConfiguravel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pe_configuracao",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_configuracao", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "pe_etapa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    referencia_guia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_etapa", x => x.id);
                    table.CheckConstraint("ck_pe_etapa_chave", "chave ~ '^[a-z][a-z0-9-]*$'");
                });

            migrationBuilder.CreateTable(
                name: "pe_modelo_historico",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entidade_id = table.Column<long>(type: "bigint", nullable: false),
                    acao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    antes = table.Column<string>(type: "jsonb", nullable: true),
                    depois = table.Column<string>(type: "jsonb", nullable: true),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_modelo_historico", x => x.id);
                    table.CheckConstraint("ck_pe_modelo_historico_acao", "acao IN ('criacao','alteracao','situacao','ordem','exclusao','remocao')");
                    table.CheckConstraint("ck_pe_modelo_historico_entidade", "entidade IN ('nivel','etapa','passo','secao','campo','opcao','orgao_nivel','orgao_ajuste')");
                });

            migrationBuilder.CreateTable(
                name: "pe_nivel",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_nivel", x => x.id);
                    table.CheckConstraint("ck_pe_nivel_codigo", "codigo ~ '^[a-z][a-z0-9_]*$'");
                });

            migrationBuilder.CreateTable(
                name: "pe_orgao_ajuste",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    alvo_tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    alvo_id = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_orgao_ajuste", x => x.id);
                    table.CheckConstraint("ck_pe_orgao_ajuste_alvo", "alvo_tipo IN ('passo','secao','campo')");
                    table.CheckConstraint("ck_pe_orgao_ajuste_situacao", "situacao IN ('obrigatorio','opcional','desligado')");
                    table.ForeignKey(
                        name: "fk_pe_orgao_ajuste_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_passo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    etapa_id = table.Column<long>(type: "bigint", nullable: false),
                    chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    o_que_fazer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    referencia_guia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    inciso_decreto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    travado = table.Column<bool>(type: "boolean", nullable: false),
                    aceita_nao_se_aplica = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_passo", x => x.id);
                    table.CheckConstraint("ck_pe_passo_chave", "chave ~ '^[a-z][a-z0-9-]*\\.[a-z0-9][a-z0-9-]*$'");
                    table.CheckConstraint("ck_pe_passo_inciso", "inciso_decreto IS NULL OR inciso_decreto ~ '^(I|II|III|IV|V|VI|VII|VIII|IX)(,(I|II|III|IV|V|VI|VII|VIII|IX))*$'");
                    table.CheckConstraint("ck_pe_passo_tipo", "tipo IN ('dados','documento','fluxo','aprovacao','envio','deliberacao','publicacao','conferencia_temas','monitoramento')");
                    table.ForeignKey(
                        name: "fk_pe_passo_etapa",
                        column: x => x.etapa_id,
                        principalTable: "pe_etapa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_orgao_config",
                columns: table => new
                {
                    orgao_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    definido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    definido_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_orgao_config", x => x.orgao_id);
                    table.ForeignKey(
                        name: "fk_pe_orgao_config_nivel",
                        column: x => x.nivel_id,
                        principalTable: "pe_nivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pe_orgao_config_orgao",
                        column: x => x.orgao_id,
                        principalTable: "pgia_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_passo_nivel",
                columns: table => new
                {
                    passo_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_passo_nivel", x => new { x.passo_id, x.nivel_id });
                    table.CheckConstraint("ck_pe_passo_nivel_situacao", "situacao IN ('obrigatorio','opcional','desligado')");
                    table.ForeignKey(
                        name: "fk_pe_passo_nivel_nivel",
                        column: x => x.nivel_id,
                        principalTable: "pe_nivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pe_passo_nivel_passo",
                        column: x => x.passo_id,
                        principalTable: "pe_passo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_secao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    passo_id = table.Column<long>(type: "bigint", nullable: true),
                    escopo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ajuda = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prefixo_codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    no_documento = table.Column<bool>(type: "boolean", nullable: false),
                    na_planilha = table.Column<bool>(type: "boolean", nullable: false),
                    travada = table.Column<bool>(type: "boolean", nullable: false),
                    inciso_decreto = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    situacao_geral = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_secao", x => x.id);
                    table.CheckConstraint("ck_pe_secao_chave", "chave ~ '^[a-z][a-z0-9_]*$'");
                    table.CheckConstraint("ck_pe_secao_escopo", "escopo IN ('pdtic','petic','df')");
                    table.CheckConstraint("ck_pe_secao_escopo_passo", "(escopo = 'pdtic') = (passo_id IS NOT NULL)");
                    table.CheckConstraint("ck_pe_secao_escopo_situacao", "(escopo = 'pdtic') = (situacao_geral IS NULL)");
                    table.CheckConstraint("ck_pe_secao_inciso", "inciso_decreto IS NULL OR inciso_decreto ~ '^(I|II|III|IV|V|VI|VII|VIII|IX)(,(I|II|III|IV|V|VI|VII|VIII|IX))*$'");
                    table.CheckConstraint("ck_pe_secao_situacao_geral", "situacao_geral IS NULL OR situacao_geral IN ('obrigatorio','opcional','desligado')");
                    table.CheckConstraint("ck_pe_secao_tipo", "tipo IN ('formulario','tabela')");
                    table.ForeignKey(
                        name: "fk_pe_secao_passo",
                        column: x => x.passo_id,
                        principalTable: "pe_passo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_campo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    secao_id = table.Column<long>(type: "bigint", nullable: false),
                    chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ajuda = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    principal = table.Column<bool>(type: "boolean", nullable: false),
                    travado = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    no_documento = table.Column<bool>(type: "boolean", nullable: false),
                    na_planilha = table.Column<bool>(type: "boolean", nullable: false),
                    largura = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    situacao_geral = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_campo", x => x.id);
                    table.CheckConstraint("ck_pe_campo_chave", "chave ~ '^[a-z][a-z0-9_]*$'");
                    table.CheckConstraint("ck_pe_campo_largura", "largura IS NULL OR largura IN ('estreita','media','larga')");
                    table.CheckConstraint("ck_pe_campo_situacao_geral", "situacao_geral IS NULL OR situacao_geral IN ('obrigatorio','opcional','desligado')");
                    table.CheckConstraint("ck_pe_campo_tipo", "tipo IN ('texto_curto','texto_longo','texto_rico','numero','moeda','percentual','data','sim_nao','lista','lista_multipla','ligacao_secao','ligacao_catalogo','arquivo','calculado')");
                    table.ForeignKey(
                        name: "fk_pe_campo_secao",
                        column: x => x.secao_id,
                        principalTable: "pe_secao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pe_secao_nivel",
                columns: table => new
                {
                    secao_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_secao_nivel", x => new { x.secao_id, x.nivel_id });
                    table.CheckConstraint("ck_pe_secao_nivel_situacao", "situacao IN ('obrigatorio','opcional','desligado')");
                    table.ForeignKey(
                        name: "fk_pe_secao_nivel_nivel",
                        column: x => x.nivel_id,
                        principalTable: "pe_nivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pe_secao_nivel_secao",
                        column: x => x.secao_id,
                        principalTable: "pe_secao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_campo_nivel",
                columns: table => new
                {
                    campo_id = table.Column<long>(type: "bigint", nullable: false),
                    nivel_id = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_campo_nivel", x => new { x.campo_id, x.nivel_id });
                    table.CheckConstraint("ck_pe_campo_nivel_situacao", "situacao IN ('obrigatorio','opcional','desligado')");
                    table.ForeignKey(
                        name: "fk_pe_campo_nivel_campo",
                        column: x => x.campo_id,
                        principalTable: "pe_campo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pe_campo_nivel_nivel",
                        column: x => x.nivel_id,
                        principalTable: "pe_nivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pe_opcao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    campo_id = table.Column<long>(type: "bigint", nullable: false),
                    valor = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    cor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    travada = table.Column<bool>(type: "boolean", nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    criado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alterado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    alterado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pe_opcao", x => x.id);
                    table.CheckConstraint("ck_pe_opcao_cor", "cor IS NULL OR cor IN ('verde','amarelo','laranja','vermelho','azul','roxo','cinza')");
                    table.CheckConstraint("ck_pe_opcao_valor", "valor ~ '^[a-z0-9][a-z0-9_]*$'");
                    table.ForeignKey(
                        name: "fk_pe_opcao_campo",
                        column: x => x.campo_id,
                        principalTable: "pe_campo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pe_campo_secao_chave",
                table: "pe_campo",
                columns: new[] { "secao_id", "chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_campo_nivel_nivel",
                table: "pe_campo_nivel",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ux_pe_etapa_chave",
                table: "pe_etapa",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_modelo_historico_data",
                table: "pe_modelo_historico",
                column: "alterado_em");

            migrationBuilder.CreateIndex(
                name: "ix_pe_modelo_historico_entidade",
                table: "pe_modelo_historico",
                columns: new[] { "entidade", "entidade_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pe_nivel_codigo",
                table: "pe_nivel",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_opcao_campo_valor",
                table: "pe_opcao",
                columns: new[] { "campo_id", "valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pe_orgao_ajuste_alvo",
                table: "pe_orgao_ajuste",
                columns: new[] { "orgao_id", "alvo_tipo", "alvo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_orgao_config_nivel",
                table: "pe_orgao_config",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_passo_etapa",
                table: "pe_passo",
                columns: new[] { "etapa_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ux_pe_passo_chave",
                table: "pe_passo",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_passo_nivel_nivel",
                table: "pe_passo_nivel",
                column: "nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_pe_secao_passo",
                table: "pe_secao",
                columns: new[] { "passo_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ux_pe_secao_chave",
                table: "pe_secao",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pe_secao_nivel_nivel",
                table: "pe_secao_nivel",
                column: "nivel_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pe_campo_nivel");

            migrationBuilder.DropTable(
                name: "pe_configuracao");

            migrationBuilder.DropTable(
                name: "pe_modelo_historico");

            migrationBuilder.DropTable(
                name: "pe_opcao");

            migrationBuilder.DropTable(
                name: "pe_orgao_ajuste");

            migrationBuilder.DropTable(
                name: "pe_orgao_config");

            migrationBuilder.DropTable(
                name: "pe_passo_nivel");

            migrationBuilder.DropTable(
                name: "pe_secao_nivel");

            migrationBuilder.DropTable(
                name: "pe_campo");

            migrationBuilder.DropTable(
                name: "pe_nivel");

            migrationBuilder.DropTable(
                name: "pe_secao");

            migrationBuilder.DropTable(
                name: "pe_passo");

            migrationBuilder.DropTable(
                name: "pe_etapa");
        }
    }
}
